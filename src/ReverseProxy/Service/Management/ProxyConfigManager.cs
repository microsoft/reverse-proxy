// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.HealthChecks;
using Yarp.ReverseProxy.Service.Proxy.Infrastructure;

namespace Yarp.ReverseProxy.Service.Management
{
    /// <summary>
    /// Provides a method to apply Proxy configuration changes
    /// by leveraging <see cref="IDynamicConfigBuilder"/>.
    /// Also an Implementation of <see cref="EndpointDataSource"/> that supports being dynamically updated
    /// in a thread-safe manner while avoiding locks on the hot path.
    /// </summary>
    /// <remarks>
    /// This takes inspiration from <a "https://github.com/dotnet/aspnetcore/blob/cbe16474ce9db7ff588aed89596ff4df5c3f62e1/src/Mvc/Mvc.Core/src/Routing/ActionEndpointDataSourceBase.cs"/>.
    /// </remarks>
    internal sealed class ProxyConfigManager : EndpointDataSource, IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly ILogger<ProxyConfigManager> _logger;
        private readonly IProxyConfigProvider _provider;
        private readonly IClusterChangeListener[] _clusterChangeListeners;
        private readonly ConcurrentDictionary<string, ClusterState> _clusters = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, RouteState> _routes = new(StringComparer.OrdinalIgnoreCase);
        private readonly IProxyConfigFilter[] _filters;
        private readonly IConfigValidator _configValidator;
        private readonly IProxyHttpClientFactory _httpClientFactory;
        private readonly ProxyEndpointFactory _proxyEndpointFactory;
        private readonly ITransformBuilder _transformBuilder;
        private readonly List<Action<EndpointBuilder>> _conventions;
        private readonly IActiveHealthCheckMonitor _activeHealthCheckMonitor;
        private IDisposable _changeSubscription;

        private List<Endpoint> _endpoints;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private IChangeToken _changeToken;

        public ProxyConfigManager(
            ILogger<ProxyConfigManager> logger,
            IProxyConfigProvider provider,
            IEnumerable<IClusterChangeListener> clusterChangeListeners,
            IEnumerable<IProxyConfigFilter> filters,
            IConfigValidator configValidator,
            ProxyEndpointFactory proxyEndpointFactory,
            ITransformBuilder transformBuilder,
            IProxyHttpClientFactory httpClientFactory,
            IActiveHealthCheckMonitor activeHealthCheckMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _clusterChangeListeners = (clusterChangeListeners as IClusterChangeListener[])
                ?? clusterChangeListeners?.ToArray() ?? throw new ArgumentNullException(nameof(clusterChangeListeners));
            _filters = (filters as IProxyConfigFilter[]) ?? filters?.ToArray() ?? throw new ArgumentNullException(nameof(filters));
            _configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator));
            _proxyEndpointFactory = proxyEndpointFactory ?? throw new ArgumentNullException(nameof(proxyEndpointFactory));
            _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _activeHealthCheckMonitor = activeHealthCheckMonitor ?? throw new ArgumentNullException(nameof(activeHealthCheckMonitor));

            _conventions = new List<Action<EndpointBuilder>>();
            DefaultBuilder = new ReverseProxyConventionBuilder(_conventions);

            _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);
        }

        public ReverseProxyConventionBuilder DefaultBuilder { get; }

        // EndpointDataSource

        /// <inheritdoc/>
        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                // The Endpoints needs to be lazy the first time to give a chance to ReverseProxyConventionBuilder to add its conventions.
                // Endpoints are accessed by routing on the first request.
                Initialize();   
                return _endpoints;
            }
        }

        private void Initialize()
        {
            if (_endpoints == null)
            {
                lock (_syncRoot)
                {
                    if (_endpoints == null)
                    {
                        CreateEndpoints();
                    }
                }
            }
        }

        private void CreateEndpoints()
        {
            var endpoints = new List<Endpoint>();
            // Directly enumerate the ConcurrentDictionary to limit locking and copying.
            foreach (var existingRoute in _routes)
            {
                // Only rebuild the endpoint for modified routes or clusters.
                var endpoint = existingRoute.Value.CachedEndpoint;
                if (endpoint == null)
                {
                    endpoint = _proxyEndpointFactory.CreateEndpoint(existingRoute.Value.Model, _conventions);
                    existingRoute.Value.CachedEndpoint = endpoint;
                }
                endpoints.Add(endpoint);
            }

            UpdateEndpoints(endpoints);
        }

        /// <inheritdoc/>
        public override IChangeToken GetChangeToken() => Volatile.Read(ref _changeToken);

        // IProxyConfigManager

        /// <inheritdoc/>
        public async Task<EndpointDataSource> InitialLoadAsync()
        {
            // Trigger the first load immediately and throw if it fails.
            // We intend this to crash the app so we don't try listening for further changes.
            try
            {
                var config = _provider.GetConfig();
                await ApplyConfigAsync(config);

                if (config.ChangeToken.ActiveChangeCallbacks)
                {
                    _changeSubscription = config.ChangeToken.RegisterChangeCallback(ReloadConfig, this);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to load or apply the proxy configuration.", ex);
            }

            // Initial active health check is run in the background.
            // Directly enumerate the ConcurrentDictionary to limit locking and copying.
            _ = _activeHealthCheckMonitor.CheckHealthAsync(_clusters.Select(pair => pair.Value));
            return this;
        }

        private static void ReloadConfig(object state)
        {
            var manager = (ProxyConfigManager)state;
            _ = manager.ReloadConfigAsync();
        }

        private async Task ReloadConfigAsync()
        {
            _changeSubscription?.Dispose();

            IProxyConfig newConfig;
            try
            {
                newConfig = _provider.GetConfig();
            }
            catch (Exception ex)
            {
                Log.ErrorReloadingConfig(_logger, ex);
                // If we can't load the config then we can't listen for changes anymore.
                return;
            }

            try
            {
                var hasChanged = await ApplyConfigAsync(newConfig);
                lock (_syncRoot)
                {
                    // Skip if changes are signaled before the endpoints are initialized for the first time.
                    // The endpoint conventions might not be ready yet.
                    if (hasChanged && _endpoints != null)
                    {
                        CreateEndpoints();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorApplyingConfig(_logger, ex);
            }

            if (newConfig.ChangeToken.ActiveChangeCallbacks)
            {
                _changeSubscription = newConfig.ChangeToken.RegisterChangeCallback(ReloadConfig, this);
            }
        }

        // Throws for validation failures
        private async Task<bool> ApplyConfigAsync(IProxyConfig config)
        {
            var (configuredRoutes, routeErrors) = await VerifyRoutesAsync(config.Routes, cancellation: default);
            var (configuredClusters, clusterErrors) = await VerifyClustersAsync(config.Clusters, cancellation: default);

            if (routeErrors.Count > 0 || clusterErrors.Count > 0)
            {
                throw new AggregateException("The proxy config is invalid.", routeErrors.Concat(clusterErrors));
            }

            // Update clusters first because routes need to reference them.
            UpdateRuntimeClusters(configuredClusters);
            var routesChanged = UpdateRuntimeRoutes(configuredRoutes);
            return routesChanged;
        }

        private async Task<(IList<RouteConfig>, IList<Exception>)> VerifyRoutesAsync(IReadOnlyList<RouteConfig> routes, CancellationToken cancellation)
        {
            if (routes == null)
            {
                return (Array.Empty<RouteConfig>(), Array.Empty<Exception>());
            }

            var seenRouteIds = new HashSet<string>();
            var configuredRoutes = new List<RouteConfig>(routes?.Count ?? 0);
            var errors = new List<Exception>();

            foreach (var r in routes)
            {
                if (seenRouteIds.Contains(r.RouteId))
                {
                    errors.Add(new ArgumentException($"Duplicate route {r.RouteId}"));
                    continue;
                }

                var route = r;

                try
                {
                    foreach (var filter in _filters)
                    {
                        route = await filter.ConfigureRouteAsync(route, cancellation);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new Exception($"An exception was thrown from the configuration callbacks for route '{r.RouteId}'.", ex));
                    continue;
                }

                var routeErrors = await _configValidator.ValidateRouteAsync(route);
                if (routeErrors.Count > 0)
                {
                    errors.AddRange(routeErrors);
                    continue;
                }

                configuredRoutes.Add(route);
            }

            if (errors.Count > 0)
            {
                return (null, errors);
            }

            return (configuredRoutes, errors);
        }

        private async Task<(IList<ClusterConfig>, IList<Exception>)> VerifyClustersAsync(IReadOnlyList<ClusterConfig> clusters, CancellationToken cancellation)
        {
            if (clusters == null)
            {
                return (Array.Empty<ClusterConfig>(), Array.Empty<Exception>());
            }

            var seenClusterIds = new HashSet<string>(clusters.Count, StringComparer.OrdinalIgnoreCase);
            var configuredClusters = new List<ClusterConfig>(clusters.Count);
            var errors = new List<Exception>();
            // The IProxyConfigProvider provides a fresh snapshot that we need to reconfigure each time.
            foreach (var c in clusters)
            {
                try
                {
                    if (seenClusterIds.Contains(c.ClusterId))
                    {
                        errors.Add(new ArgumentException($"Duplicate cluster '{c.ClusterId}'."));
                        continue;
                    }

                    seenClusterIds.Add(c.ClusterId);

                    // Don't modify the original
                    var cluster = c;

                    foreach (var filter in _filters)
                    {
                        cluster = await filter.ConfigureClusterAsync(cluster, cancellation);
                    }

                    var clusterErrors = await _configValidator.ValidateClusterAsync(cluster);
                    if (clusterErrors.Count > 0)
                    {
                        errors.AddRange(clusterErrors);
                        continue;
                    }

                    configuredClusters.Add(cluster);
                }
                catch (Exception ex)
                {
                    errors.Add(new ArgumentException($"An exception was thrown from the configuration callbacks for cluster '{c.ClusterId}'.", ex));
                }
            }

            if (errors.Count > 0)
            {
                return (null, errors);
            }

            return (configuredClusters, errors);
        }

        private void UpdateRuntimeClusters(IList<ClusterConfig> incomingClusters)
        {
            var desiredClusters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var incomingCluster in incomingClusters)
            {
                desiredClusters.Add(incomingCluster.ClusterId);

                if (_clusters.TryGetValue(incomingCluster.ClusterId, out var currentCluster))
                {
                    var destinationsChanged = UpdateRuntimeDestinations(incomingCluster.Destinations, currentCluster.Destinations);

                    var currentClusterModel = currentCluster.Model;

                    var httpClient = _httpClientFactory.CreateClient(new ProxyHttpClientContext
                    {
                        ClusterId = currentCluster.ClusterId,
                        OldConfig = currentClusterModel.Config.HttpClient ?? HttpClientConfig.Empty,
                        OldMetadata = currentClusterModel.Config.Metadata,
                        OldClient = currentClusterModel.HttpClient,
                        NewConfig = incomingCluster.HttpClient ?? HttpClientConfig.Empty,
                        NewMetadata = incomingCluster.Metadata
                    });

                    var newClusterModel = new ClusterModel(incomingCluster, httpClient);

                    // Excludes destination changes, they're tracked separately.
                    var configChanged = currentClusterModel.HasConfigChanged(newClusterModel);
                    if (configChanged)
                    {
                        currentCluster.Revision++;
                        Log.ClusterChanged(_logger, incomingCluster.ClusterId);

                        // Config changed, so update runtime cluster
                        currentCluster.Model = newClusterModel;
                    }

                    if (destinationsChanged || configChanged)
                    {
                        currentCluster.ProcessDestinationChanges();

                        foreach (var listener in _clusterChangeListeners)
                        {
                            listener.OnClusterChanged(currentCluster);
                        }
                    }
                }
                else
                {
                    var newClusterState = new ClusterState(incomingCluster.ClusterId);

                    UpdateRuntimeDestinations(incomingCluster.Destinations, newClusterState.Destinations);

                    var httpClient = _httpClientFactory.CreateClient(new ProxyHttpClientContext
                    {
                        ClusterId = newClusterState.ClusterId,
                        NewConfig = incomingCluster.HttpClient ?? HttpClientConfig.Empty,
                        NewMetadata = incomingCluster.Metadata
                    });

                    newClusterState.Model = new ClusterModel(incomingCluster, httpClient);
                    newClusterState.Revision++;
                    Log.ClusterAdded(_logger, incomingCluster.ClusterId);

                    newClusterState.ProcessDestinationChanges();

                    var added = _clusters.TryAdd(newClusterState.ClusterId, newClusterState);
                    Debug.Assert(added);

                    foreach (var listener in _clusterChangeListeners)
                    {
                        listener.OnClusterAdded(newClusterState);
                    }
                }
            }

            // Directly enumerate the ConcurrentDictionary to limit locking and copying.
            foreach (var existingClusterPair in _clusters)
            {
                var existingCluster = existingClusterPair.Value;
                if (!desiredClusters.Contains(existingCluster.ClusterId))
                {
                    // NOTE 1: Remove is safe to do within the `foreach` loop on ConcurrentDictionary
                    //
                    // NOTE 2: Removing the cluster from _clusters is safe and existing
                    // ASP .NET Core endpoints will continue to work with their existing behavior (until those endpoints are updated)
                    // and the Garbage Collector won't destroy this cluster object while it's referenced elsewhere.
                    Log.ClusterRemoved(_logger, existingCluster.ClusterId);
                    var removed = _clusters.TryRemove(existingCluster.ClusterId, out var _);
                    Debug.Assert(removed);

                    foreach (var listener in _clusterChangeListeners)
                    {
                        listener.OnClusterRemoved(existingCluster);
                    }
                }
            }
        }

        private bool UpdateRuntimeDestinations(IReadOnlyDictionary<string, DestinationConfig> incomingDestinations, ConcurrentDictionary<string, DestinationState> currentDestinations)
        {
            var desiredDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var incomingDestination in incomingDestinations)
            {
                desiredDestinations.Add(incomingDestination.Key);

                if (currentDestinations.TryGetValue(incomingDestination.Key, out var currentDestination))
                {
                    if (currentDestination.Model.HasChanged(incomingDestination.Value))
                    {
                        Log.DestinationChanged(_logger, incomingDestination.Key);
                        currentDestination.Model = new DestinationModel(incomingDestination.Value);
                        changed = true;
                    }
                }
                else
                {
                    Log.DestinationAdded(_logger, incomingDestination.Key);
                    var newDestination = new DestinationState(incomingDestination.Key)
                    {
                        Model = new DestinationModel(incomingDestination.Value),
                    };
                    var added = currentDestinations.TryAdd(newDestination.DestinationId, newDestination);
                    Debug.Assert(added);
                    changed = true;
                }
            }

            // Directly enumerate the ConcurrentDictionary to limit locking and copying.
            foreach (var existingDestinationPair in currentDestinations)
            {
                var id = existingDestinationPair.Value.DestinationId;
                if (!desiredDestinations.Contains(id))
                {
                    // NOTE 1: Remove is safe to do within the `foreach` loop on ConcurrentDictionary
                    //
                    // NOTE 2: Removing the endpoint from `IEndpointManager` is safe and existing
                    // clusters will continue to work with their existing behavior (until those clusters are updated)
                    // and the Garbage Collector won't destroy this cluster object while it's referenced elsewhere.
                    Log.DestinationRemoved(_logger, id);
                    var removed = currentDestinations.TryRemove(id, out var _);
                    Debug.Assert(removed);
                    changed = true;
                }
            }

            return changed;
        }

        private bool UpdateRuntimeRoutes(IList<RouteConfig> incomingRoutes)
        {
            var desiredRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var incomingRoute in incomingRoutes)
            {
                desiredRoutes.Add(incomingRoute.RouteId);

                // Note that this can be null, and that is fine. The resulting route may match
                // but would then fail to route, which is exactly what we were instructed to do in this case
                // since no valid cluster was specified.
                _clusters.TryGetValue(incomingRoute.ClusterId ?? string.Empty, out var cluster);

                if (_routes.TryGetValue(incomingRoute.RouteId, out var currentRoute))
                {
                    if (currentRoute.Model.HasConfigChanged(incomingRoute, cluster, currentRoute.ClusterRevision))
                    {
                        currentRoute.CachedEndpoint = null; // Recreate endpoint
                        var newModel = BuildRouteModel(incomingRoute, cluster);
                        currentRoute.Model = newModel;
                        currentRoute.ClusterRevision = cluster?.Revision;
                        changed = true;
                        Log.RouteChanged(_logger, currentRoute.RouteId);
                    }
                }
                else
                {
                    var newModel = BuildRouteModel(incomingRoute, cluster);
                    var newState = new RouteState(incomingRoute.RouteId)
                    {
                        Model = newModel,
                        ClusterRevision = cluster?.Revision,
                    };
                    var added = _routes.TryAdd(newState.RouteId, newState);
                    Debug.Assert(added);
                    changed = true;
                    Log.RouteAdded(_logger, newState.RouteId);
                }
            }

            // Directly enumerate the ConcurrentDictionary to limit locking and copying.
            foreach (var existingRoutePair in _routes)
            {
                var routeId = existingRoutePair.Value.RouteId;
                if (!desiredRoutes.Contains(routeId))
                {
                    // NOTE 1: Remove is safe to do within the `foreach` loop on ConcurrentDictionary
                    //
                    // NOTE 2: Removing the route from _routes is safe and existing
                    // ASP .NET Core endpoints will continue to work with their existing behavior since
                    // their copy of `RouteModel` is immutable and remains operational in whichever state is was in.
                    Log.RouteRemoved(_logger, routeId);
                    var removed = _routes.TryRemove(routeId, out var _);
                    Debug.Assert(removed);
                    changed = true;
                }
            }

            return changed;
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

        private RouteModel BuildRouteModel(RouteConfig source, ClusterState cluster)
        {
            var transforms = _transformBuilder.Build(source, cluster?.Model?.Config);

            return new RouteModel(source, cluster, transforms);
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
                  "Cluster '{clusterId}' has been added.");

            private static readonly Action<ILogger, string, Exception> _clusterChanged = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.ClusterChanged,
                "Cluster '{clusterId}' has changed.");

            private static readonly Action<ILogger, string, Exception> _clusterRemoved = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.ClusterRemoved,
                "Cluster '{clusterId}' has been removed.");

            private static readonly Action<ILogger, string, Exception> _destinationAdded = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.DestinationAdded,
                "Destination '{destinationId}' has been added.");

            private static readonly Action<ILogger, string, Exception> _destinationChanged = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.DestinationChanged,
                "Destination '{destinationId}' has changed.");

            private static readonly Action<ILogger, string, Exception> _destinationRemoved = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.DestinationRemoved,
                "Destination '{destinationId}' has been removed.");

            private static readonly Action<ILogger, string, Exception> _routeAdded = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.RouteAdded,
                "Route '{routeId}' has been added.");

            private static readonly Action<ILogger, string, Exception> _routeChanged = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.RouteChanged,
                "Route '{routeId}' has changed.");

            private static readonly Action<ILogger, string, Exception> _routeRemoved = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.RouteRemoved,
                "Route '{routeId}' has been removed.");

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
