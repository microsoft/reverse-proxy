// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Utilities;
using Microsoft.Extensions.Logging;
using AspNetCore = Microsoft.AspNetCore;

namespace IslandGateway.Core.Service.Management
{
    /// <summary>
    /// Default implementation of <see cref="IIslandGatewayConfigManager"/>
    /// which provides a method to apply Gateway configuration changes
    /// by leveraging <see cref="IDynamicConfigBuilder"/>.
    /// </summary>
    internal class IslandGatewayConfigManager : IIslandGatewayConfigManager
    {
        private readonly ILogger<IslandGatewayConfigManager> _logger;
        private readonly IDynamicConfigBuilder _configBuilder;
        private readonly IRuntimeRouteBuilder _routeEndpointBuilder;
        private readonly IBackendManager _backendManager;
        private readonly IRouteManager _routeManager;
        private readonly IGatewayDynamicEndpointDataSource _dynamicEndpointDataSource;

        public IslandGatewayConfigManager(
            ILogger<IslandGatewayConfigManager> logger,
            IDynamicConfigBuilder configBuilder,
            IRuntimeRouteBuilder routeEndpointBuilder,
            IBackendManager backendManager,
            IRouteManager routeManager,
            IGatewayDynamicEndpointDataSource dynamicEndpointDataSource)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(configBuilder, nameof(configBuilder));
            Contracts.CheckValue(routeEndpointBuilder, nameof(routeEndpointBuilder));
            Contracts.CheckValue(backendManager, nameof(backendManager));
            Contracts.CheckValue(routeManager, nameof(routeManager));
            Contracts.CheckValue(dynamicEndpointDataSource, nameof(dynamicEndpointDataSource));

            _logger = logger;
            _configBuilder = configBuilder;
            _routeEndpointBuilder = routeEndpointBuilder;
            _backendManager = backendManager;
            _routeManager = routeManager;
            _dynamicEndpointDataSource = dynamicEndpointDataSource;
        }

        /// <inheritdoc/>
        public async Task<bool> ApplyConfigurationsAsync(IConfigErrorReporter configErrorReporter, CancellationToken cancellation)
        {
            if (configErrorReporter == null)
            {
                throw new ArgumentNullException(nameof(configErrorReporter));
            }

            var configResult = await _configBuilder.BuildConfigAsync(configErrorReporter, cancellation);
            if (!configResult.IsSuccess)
            {
                return false;
            }

            var config = configResult.Value;
            UpdateRuntimeBackends(config);
            UpdateRuntimeRoutes(config);

            return true;
        }

        private void UpdateRuntimeBackends(DynamicConfigRoot config)
        {
            var desiredBackends = new HashSet<string>(StringComparer.Ordinal);
            foreach (var configBackendWithEndpoints in config.Backends)
            {
                var configBackend = configBackendWithEndpoints.Backend;
                desiredBackends.Add(configBackend.BackendId);

                _backendManager.GetOrCreateItem(
                    itemId: configBackend.BackendId,
                    setupAction: backend =>
                    {
                        UpdateRuntimeEndpoints(configBackendWithEndpoints.Endpoints, backend.EndpointManager);

                        var newConfig = new BackendConfig(
                                new BackendConfig.BackendHealthCheckOptions(
                                    enabled: configBackend.HealthCheckOptions?.Enabled ?? false,
                                    interval: configBackend.HealthCheckOptions?.Interval ?? TimeSpan.FromSeconds(0),
                                    timeout: configBackend.HealthCheckOptions?.Timeout ?? TimeSpan.FromSeconds(0),
                                    port: configBackend.HealthCheckOptions?.Port ?? 0,
                                    path: configBackend.HealthCheckOptions?.Path ?? string.Empty),
                                new BackendConfig.BackendLoadBalancingOptions(
                                    mode: BackendConfig.BackendLoadBalancingOptions.LoadBalancingMode.First));

                        var currentBackendConfig = backend.Config.Value;
                        if (currentBackendConfig == null ||
                            currentBackendConfig.HealthCheckOptions.Enabled != newConfig.HealthCheckOptions.Enabled ||
                            currentBackendConfig.HealthCheckOptions.Interval != newConfig.HealthCheckOptions.Interval ||
                            currentBackendConfig.HealthCheckOptions.Timeout != newConfig.HealthCheckOptions.Timeout ||
                            currentBackendConfig.HealthCheckOptions.Port != newConfig.HealthCheckOptions.Port ||
                            currentBackendConfig.HealthCheckOptions.Path != newConfig.HealthCheckOptions.Path)
                        {
                            // Config changed, so update runtime backend
                            backend.Config.Value = newConfig;
                        }
                    });
            }

            foreach (var existingBackend in _backendManager.GetItems())
            {
                if (!desiredBackends.Contains(existingBackend.BackendId))
                {
                    // NOTE 1: This is safe to do within the `foreach` loop
                    // because `IBackendManager.GetItems` returns a copy of the list of backends.
                    //
                    // NOTE 2: Removing the backend from `IBackendManager` is safe and existing
                    // ASP .NET Core endpoints will continue to work with their existing behavior (until those endpoints are updated)
                    // and the Garbage Collector won't destroy this backend object while it's referenced elsewhere.
                    _backendManager.TryRemoveItem(existingBackend.BackendId);
                }
            }
        }

        private void UpdateRuntimeEndpoints(IList<BackendEndpoint> configEndpoints, IEndpointManager endpointManager)
        {
            var desiredEndpoints = new HashSet<string>(StringComparer.Ordinal);
            foreach (var configEndpoint in configEndpoints)
            {
                desiredEndpoints.Add(configEndpoint.EndpointId);
                endpointManager.GetOrCreateItem(
                    itemId: configEndpoint.EndpointId,
                    setupAction: endpoint =>
                    {
                        if (endpoint.Config.Value?.Address != configEndpoint.Address)
                        {
                            endpoint.Config.Value = new EndpointConfig(configEndpoint.Address);
                        }
                    });
            }

            foreach (var existingEndpoint in endpointManager.GetItems())
            {
                if (!desiredEndpoints.Contains(existingEndpoint.EndpointId))
                {
                    // NOTE 1: This is safe to do within the `foreach` loop
                    // because `IEndpointManager.GetItems` returns a copy of the list of endpoints.
                    //
                    // NOTE 2: Removing the endpoint from `IEndpointManager` is safe and existing
                    // backends will continue to work with their existing behavior (until those backends are updated)
                    // and the Garbage Collector won't destroy this backend object while it's referenced elsewhere.
                    endpointManager.TryRemoveItem(existingEndpoint.EndpointId);
                }
            }
        }

        private void UpdateRuntimeRoutes(DynamicConfigRoot config)
        {
            var desiredRoutes = new HashSet<string>(StringComparer.Ordinal);
            var changed = false;

            foreach (var configRoute in config.Routes)
            {
                desiredRoutes.Add(configRoute.RouteId);

                // Note that this can be null, and that is fine. The resulting route may match
                // but would then fail to route, which is exactly what we were instructed to do in this case
                // since no valid backend was specified.
                var backendOrNull = _backendManager.TryGetItem(configRoute.BackendId);

                _routeManager.GetOrCreateItem(
                    itemId: configRoute.RouteId,
                    setupAction: route =>
                    {
                        var currentRouteConfig = route.Config.Value;
                        if (currentRouteConfig == null ||
                            currentRouteConfig.Rule != configRoute.Rule ||
                            currentRouteConfig.Priority != configRoute.Priority ||
                            currentRouteConfig.BackendOrNull != backendOrNull)
                        {
                            // Config changed, so update runtime route
                            changed = true;

                            var newConfig = _routeEndpointBuilder.Build(configRoute, backendOrNull, route);
                            route.Config.Value = newConfig;
                        }
                    });
            }

            foreach (var existingRoute in _routeManager.GetItems())
            {
                if (!desiredRoutes.Contains(existingRoute.RouteId))
                {
                    // NOTE 1: This is safe to do within the `foreach` loop
                    // because `IRouteManager.GetItems` returns a copy of the list of routes.
                    //
                    // NOTE 2: Removing the route from `IRouteManager` is safe and existing
                    // ASP .NET Core endpoints will continue to work with their existing behavior since
                    // their copy of `RouteConfig` is immutable and remains operational in whichever state is was in.
                    _routeManager.TryRemoveItem(existingRoute.RouteId);
                    changed = true;
                }
            }

            if (changed)
            {
                var aspNetCoreEndpoints = new List<AspNetCore.Http.Endpoint>();
                foreach (var existingRoute in _routeManager.GetItems())
                {
                    var runtimeConfig = existingRoute.Config.Value;
                    if (runtimeConfig?.AspNetCoreEndpoints != null)
                    {
                        aspNetCoreEndpoints.AddRange(runtimeConfig.AspNetCoreEndpoints);
                    }
                }

                // This is where the new routes take effect!
                _dynamicEndpointDataSource.Update(aspNetCoreEndpoints);
            }
        }
    }
}
