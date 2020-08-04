// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Default implementation of <see cref="IProxyConfigManager"/>
    /// which provides a method to apply Proxy configuration changes
    /// by leveraging <see cref="IDynamicConfigBuilder"/>.
    /// Also an Implementation of <see cref="EndpointDataSource"/> that supports being dynamically updated
    /// in a thread-safe manner while avoiding locks on the hot path.
    /// </summary>
    /// <remarks>
    /// This takes inspiration from <a href="https://github.com/aspnet/AspNetCore/blob/master/src/Mvc/Mvc.Core/src/Routing/ActionEndpointDataSourceBase.cs"/>.
    /// </remarks>
    internal class ProxyConfigManager : EndpointDataSource, IProxyConfigManager, IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly ILogger<ProxyConfigManager> _logger;
        private readonly IProxyConfigProvider _provider;
        private readonly IDynamicConfigBuilder _configBuilder;
        private readonly IRuntimeRouteBuilder _routeEndpointBuilder;
        private readonly IClusterManager _clusterManager;
        private readonly IRouteManager _routeManager;
        private IDisposable _changeSubscription;

        private List<Endpoint> _endpoints = new List<Endpoint>(0);
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private IChangeToken _changeToken;

        public ProxyConfigManager(
            ILogger<ProxyConfigManager> logger,
            IProxyConfigProvider provider,
            IDynamicConfigBuilder configBuilder,
            IRuntimeRouteBuilder routeEndpointBuilder,
            IClusterManager clusterManager,
            IRouteManager routeManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _configBuilder = configBuilder ?? throw new ArgumentNullException(nameof(configBuilder));
            _routeEndpointBuilder = routeEndpointBuilder ?? throw new ArgumentNullException(nameof(routeEndpointBuilder));
            _clusterManager = clusterManager ?? throw new ArgumentNullException(nameof(clusterManager));
            _routeManager = routeManager ?? throw new ArgumentNullException(nameof(routeManager));
            _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);
        }

        // EndpointDataSource

        /// <inheritdoc/>
        public override IReadOnlyList<Endpoint> Endpoints => Volatile.Read(ref _endpoints);

        /// <inheritdoc/>
        public override IChangeToken GetChangeToken() => Volatile.Read(ref _changeToken);

        // IProxyConfigManager

        /// <inheritdoc/>
        public async Task<EndpointDataSource> LoadAsync()
        {
            // Trigger the first load immediately and throw if it fails.
            // We intend this to crash the app so we don't try listening for further changes.
            var config = _provider.GetConfig();
            await ApplyConfigAsync(config);

            if (config.ChangeToken.ActiveChangeCallbacks)
            {
                _changeSubscription = config.ChangeToken.RegisterChangeCallback(ReloadConfigAsync, this);
            }

            return this;
        }

        // Throws for validation failures
        private async Task ApplyConfigAsync(IProxyConfig config)
        {
            var dynamicConfig = await _configBuilder.BuildConfigAsync(config.Routes, config.Clusters, default);

            UpdateRuntimeClusters(dynamicConfig);
            UpdateRuntimeRoutes(dynamicConfig);
        }

        private static async void ReloadConfigAsync(object state)
        {
            var manager = (ProxyConfigManager)state;

            IProxyConfig newConfig;
            try
            {
                newConfig = manager._provider.GetConfig();
            }
            catch (Exception ex)
            {
                Log.ErrorReloadingConfig(manager._logger, ex);
                // If we can't load the config then we can't listen for changes anymore.
                return;
            }

            try
            {
                await manager.ApplyConfigAsync(newConfig);
            }
            catch (Exception ex)
            {
                Log.ErrorApplyingConfig(manager._logger, ex);
            }

            if (newConfig.ChangeToken.ActiveChangeCallbacks)
            {
                manager._changeSubscription?.Dispose();
                manager._changeSubscription = newConfig.ChangeToken.RegisterChangeCallback(ReloadConfigAsync, manager);
            }
        }

        private void UpdateRuntimeClusters(DynamicConfigRoot config)
        {
            var desiredClusters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var configClusterPair in config.Clusters)
            {
                var configCluster = configClusterPair.Value;
                desiredClusters.Add(configClusterPair.Key);

                _clusterManager.GetOrCreateItem(
                    itemId: configClusterPair.Key,
                    setupAction: cluster =>
                    {
                        UpdateRuntimeDestinations(configCluster.Destinations, cluster.DestinationManager);

                        var newConfig = new ClusterConfig(
                                new ClusterConfig.ClusterHealthCheckOptions(
                                    enabled: configCluster.HealthCheckOptions?.Enabled ?? false,
                                    interval: configCluster.HealthCheckOptions?.Interval ?? TimeSpan.FromSeconds(0),
                                    timeout: configCluster.HealthCheckOptions?.Timeout ?? TimeSpan.FromSeconds(0),
                                    port: configCluster.HealthCheckOptions?.Port ?? 0,
                                    path: configCluster.HealthCheckOptions?.Path ?? string.Empty),
                                new ClusterConfig.ClusterLoadBalancingOptions(
                                    mode: configCluster.LoadBalancing?.Mode ?? default),
                                new ClusterConfig.ClusterSessionAffinityOptions(
                                    enabled: configCluster.SessionAffinity?.Enabled ?? false,
                                    mode: configCluster.SessionAffinity?.Mode,
                                    failurePolicy: configCluster.SessionAffinity?.FailurePolicy,
                                    settings: configCluster.SessionAffinity?.Settings as IReadOnlyDictionary<string, string>));

                        var currentClusterConfig = cluster.Config.Value;
                        if (currentClusterConfig == null ||
                            currentClusterConfig.HealthCheckOptions.Enabled != newConfig.HealthCheckOptions.Enabled ||
                            currentClusterConfig.HealthCheckOptions.Interval != newConfig.HealthCheckOptions.Interval ||
                            currentClusterConfig.HealthCheckOptions.Timeout != newConfig.HealthCheckOptions.Timeout ||
                            currentClusterConfig.HealthCheckOptions.Port != newConfig.HealthCheckOptions.Port ||
                            currentClusterConfig.HealthCheckOptions.Path != newConfig.HealthCheckOptions.Path)
                        {
                            if (currentClusterConfig == null)
                            {
                                Log.ClusterAdded(_logger, configClusterPair.Key);
                            }
                            else
                            {
                                Log.ClusterChanged(_logger, configClusterPair.Key);
                            }

                            // Config changed, so update runtime cluster
                            cluster.Config.Value = newConfig;
                        }
                    });
            }

            foreach (var existingCluster in _clusterManager.GetItems())
            {
                if (!desiredClusters.Contains(existingCluster.ClusterId))
                {
                    // NOTE 1: This is safe to do within the `foreach` loop
                    // because `IClusterManager.GetItems` returns a copy of the list of clusters.
                    //
                    // NOTE 2: Removing the cluster from `IClusterManager` is safe and existing
                    // ASP .NET Core endpoints will continue to work with their existing behavior (until those endpoints are updated)
                    // and the Garbage Collector won't destroy this cluster object while it's referenced elsewhere.
                    Log.ClusterRemoved(_logger, existingCluster.ClusterId);
                    _clusterManager.TryRemoveItem(existingCluster.ClusterId);
                }
            }
        }

        private void UpdateRuntimeDestinations(IDictionary<string, Destination> configDestinations, IDestinationManager destinationManager)
        {
            var desiredDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var configDestination in configDestinations)
            {
                desiredDestinations.Add(configDestination.Key);
                destinationManager.GetOrCreateItem(
                    itemId: configDestination.Key,
                    setupAction: destination =>
                    {
                        var destinationConfig = destination.ConfigSignal.Value;
                        if (destinationConfig?.Address != configDestination.Value.Address)
                        {
                            if (destinationConfig == null)
                            {
                                Log.DestinationAdded(_logger, configDestination.Key);
                            }
                            else
                            {
                                Log.DestinationChanged(_logger, configDestination.Key);
                            }
                            destination.ConfigSignal.Value = new DestinationConfig(configDestination.Value.Address);
                        }
                    });
            }

            foreach (var existingDestination in destinationManager.GetItems())
            {
                if (!desiredDestinations.Contains(existingDestination.DestinationId))
                {
                    // NOTE 1: This is safe to do within the `foreach` loop
                    // because `IDestinationManager.GetItems` returns a copy of the list of destinations.
                    //
                    // NOTE 2: Removing the endpoint from `IEndpointManager` is safe and existing
                    // clusters will continue to work with their existing behavior (until those clusters are updated)
                    // and the Garbage Collector won't destroy this cluster object while it's referenced elsewhere.
                    Log.DestinationRemoved(_logger, existingDestination.DestinationId);
                    destinationManager.TryRemoveItem(existingDestination.DestinationId);
                }
            }
        }

        private void UpdateRuntimeRoutes(DynamicConfigRoot config)
        {
            var desiredRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var configRoute in config.Routes)
            {
                desiredRoutes.Add(configRoute.RouteId);

                // Note that this can be null, and that is fine. The resulting route may match
                // but would then fail to route, which is exactly what we were instructed to do in this case
                // since no valid cluster was specified.
                var cluster = _clusterManager.TryGetItem(configRoute.ClusterId ?? string.Empty);

                _routeManager.GetOrCreateItem(
                    itemId: configRoute.RouteId,
                    setupAction: route =>
                    {
                        var currentRouteConfig = route.Config.Value;
                        if (currentRouteConfig == null ||
                            currentRouteConfig.HasConfigChanged(configRoute, cluster))
                        {
                            // Config changed, so update runtime route
                            changed = true;
                            if (currentRouteConfig == null)
                            {
                                Log.RouteAdded(_logger, configRoute.RouteId);
                            }
                            else
                            {
                                Log.RouteChanged(_logger, configRoute.RouteId);
                            }

                            var newConfig = _routeEndpointBuilder.Build(configRoute, cluster, route);
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
                    Log.RouteRemoved(_logger, existingRoute.RouteId);
                    _routeManager.TryRemoveItem(existingRoute.RouteId);
                    changed = true;
                }
            }

            if (changed)
            {
                var endpoints = new List<Endpoint>();
                foreach (var existingRoute in _routeManager.GetItems())
                {
                    var runtimeConfig = existingRoute.Config.Value;
                    if (runtimeConfig?.Endpoints != null)
                    {
                        endpoints.AddRange(runtimeConfig.Endpoints);
                    }
                }

                UpdateEndpoints(endpoints);
            }
        }

        /// <summary>
        /// Applies a new set of ASP .NET Core endpoints. Changes take effect immediately.
        /// </summary>
        /// <param name="endpoints">New endpoints to apply.</param>
        private void UpdateEndpoints(List<Endpoint> endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            lock (_syncRoot)
            {
                // These steps are done in a specific order to ensure callers always see a consistent state.

                // Step 1 - capture old token
                var oldCancellationTokenSource = _cancellationTokenSource;

                // Step 2 - update endpoints
                Volatile.Write(ref _endpoints, endpoints);

                // Step 3 - create new change token
                _cancellationTokenSource = new CancellationTokenSource();
                Volatile.Write(ref _changeToken, new CancellationChangeToken(_cancellationTokenSource.Token));

                // Step 4 - trigger old token
                oldCancellationTokenSource?.Cancel();
            }
        }

        public void Dispose()
        {
            _changeSubscription?.Dispose();
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _clusterAdded = LoggerMessage.Define<string>(
                  LogLevel.Debug,
                  EventIds.ClusterAdded,
                  "Cluster `{clusterId}` has been added.");

            private static readonly Action<ILogger, string, Exception> _clusterChanged = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.ClusterChanged,
                "Cluster `{clusterId}` has changed.");

            private static readonly Action<ILogger, string, Exception> _clusterRemoved = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.ClusterRemoved,
                "Cluster `{clusterId}` has been removed.");

            private static readonly Action<ILogger, string, Exception> _destinationAdded = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.DestinationAdded,
                "Destination `{destinationId}` has been added.");

            private static readonly Action<ILogger, string, Exception> _destinationChanged = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.DestinationChanged,
                "Destination `{destinationId}` has changed.");

            private static readonly Action<ILogger, string, Exception> _destinationRemoved = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.DestinationRemoved,
                "Destination `{destinationId}` has been removed.");

            private static readonly Action<ILogger, string, Exception> _routeAdded = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.RouteAdded,
                "Route `{routeId}` has been added.");

            private static readonly Action<ILogger, string, Exception> _routeChanged = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.RouteChanged,
                "Route `{routeId}` has changed.");

            private static readonly Action<ILogger, string, Exception> _routeRemoved = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.RouteRemoved,
                "Route `{routeId}` has been removed.");

            private static readonly Action<ILogger, Exception> _errorReloadingConfig = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.ErrorReloadingConfig,
                "Failed to reload config. Unable to listen for future changes.");

            private static readonly Action<ILogger, Exception> _errorApplyingConfig = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.ErrorApplyingConfig,
                "Failed to apply the new config.");

            public static void ClusterAdded(ILogger logger, string clusterId)
            {
                _clusterAdded(logger, clusterId, null);
            }

            public static void ClusterChanged(ILogger logger, string clusterId)
            {
                _clusterChanged(logger, clusterId, null);
            }

            public static void ClusterRemoved(ILogger logger, string clusterId)
            {
                _clusterRemoved(logger, clusterId, null);
            }

            public static void DestinationAdded(ILogger logger, string destinationId)
            {
                _destinationAdded(logger, destinationId, null);
            }

            public static void DestinationChanged(ILogger logger, string destinationId)
            {
                _destinationChanged(logger, destinationId, null);
            }

            public static void DestinationRemoved(ILogger logger, string destinationId)
            {
                _destinationRemoved(logger, destinationId, null);
            }

            public static void RouteAdded(ILogger logger, string routeId)
            {
                _routeAdded(logger, routeId, null);
            }

            public static void RouteChanged(ILogger logger, string routeId)
            {
                _routeChanged(logger, routeId, null);
            }

            public static void RouteRemoved(ILogger logger, string routeId)
            {
                _routeRemoved(logger, routeId, null);
            }

            public static void ErrorReloadingConfig(ILogger logger, Exception ex)
            {
                _errorReloadingConfig(logger, ex);
            }

            public static void ErrorApplyingConfig(ILogger logger, Exception ex)
            {
                _errorApplyingConfig(logger, ex);
            }
        }
    }
}
