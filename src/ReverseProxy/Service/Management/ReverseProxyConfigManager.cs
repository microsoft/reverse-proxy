// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Default implementation of <see cref="IReverseProxyConfigManager"/>
    /// which provides a method to apply Proxy configuration changes
    /// by leveraging <see cref="IDynamicConfigBuilder"/>.
    /// </summary>
    internal class ReverseProxyConfigManager : IReverseProxyConfigManager
    {
        private readonly ILogger<ReverseProxyConfigManager> _logger;
        private readonly IDynamicConfigBuilder _configBuilder;
        private readonly IRuntimeRouteBuilder _routeEndpointBuilder;
        private readonly IClusterManager _clusterManager;
        private readonly IRouteManager _routeManager;
        private readonly IProxyDynamicEndpointDataSource _dynamicEndpointDataSource;

        public ReverseProxyConfigManager(
            ILogger<ReverseProxyConfigManager> logger,
            IDynamicConfigBuilder configBuilder,
            IRuntimeRouteBuilder routeEndpointBuilder,
            IClusterManager clusterManager,
            IRouteManager routeManager,
            IProxyDynamicEndpointDataSource dynamicEndpointDataSource)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(configBuilder, nameof(configBuilder));
            Contracts.CheckValue(routeEndpointBuilder, nameof(routeEndpointBuilder));
            Contracts.CheckValue(clusterManager, nameof(clusterManager));
            Contracts.CheckValue(routeManager, nameof(routeManager));
            Contracts.CheckValue(dynamicEndpointDataSource, nameof(dynamicEndpointDataSource));

            _logger = logger;
            _configBuilder = configBuilder;
            _routeEndpointBuilder = routeEndpointBuilder;
            _clusterManager = clusterManager;
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
            UpdateRuntimeClusters(config);
            UpdateRuntimeRoutes(config);

            return true;
        }

        private void UpdateRuntimeClusters(DynamicConfigRoot config)
        {
            var desiredClusters = new HashSet<string>(StringComparer.Ordinal);
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
            var desiredDestinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var configDestination in configDestinations)
            {
                desiredDestinations.Add(configDestination.Key);
                destinationManager.GetOrCreateItem(
                    itemId: configDestination.Key,
                    setupAction: destination =>
                    {
                        if (destination.Config.Value?.Address != configDestination.Value.Address)
                        {
                            if (destination.Config.Value == null)
                            {
                                Log.DestinationAdded(_logger, configDestination.Key);
                            }
                            else
                            {
                                Log.DestinationChanged(_logger, configDestination.Key);
                            }
                            destination.Config.Value = new DestinationConfig(configDestination.Value.Address);
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
            var desiredRoutes = new HashSet<string>(StringComparer.Ordinal);
            var changed = false;

            foreach (var configRoute in config.Routes)
            {
                desiredRoutes.Add(configRoute.RouteId);

                // Note that this can be null, and that is fine. The resulting route may match
                // but would then fail to route, which is exactly what we were instructed to do in this case
                // since no valid cluster was specified.
                var clusterOrNull = _clusterManager.TryGetItem(configRoute.ClusterId);

                _routeManager.GetOrCreateItem(
                    itemId: configRoute.RouteId,
                    setupAction: route =>
                    {
                        var currentRouteConfig = route.Config.Value;
                        if (currentRouteConfig == null ||
                            currentRouteConfig.HasConfigChanged(configRoute, clusterOrNull))
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

                            var newConfig = _routeEndpointBuilder.Build(configRoute, clusterOrNull, route);
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

                // This is where the new routes take effect!
                _dynamicEndpointDataSource.Update(endpoints);
            }
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
        }
    }
}
