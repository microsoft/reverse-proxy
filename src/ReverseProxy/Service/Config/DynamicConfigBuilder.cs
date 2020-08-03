// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.Service.SessionAffinity;

namespace Microsoft.ReverseProxy.Service
{
    internal class DynamicConfigBuilder : IDynamicConfigBuilder
    {
        private readonly IEnumerable<IProxyConfigFilter> _filters;
        private readonly IRouteValidator _parsedRouteValidator;
        private readonly ILogger<DynamicConfigBuilder> _logger;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;
        private readonly IDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;

        public DynamicConfigBuilder(
            IEnumerable<IProxyConfigFilter> filters,
            IRouteValidator parsedRouteValidator,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies,
            ILogger<DynamicConfigBuilder> logger)
        {
            _filters = filters ?? throw new ArgumentNullException(nameof(filters));
            _parsedRouteValidator = parsedRouteValidator ?? throw new ArgumentNullException(nameof(parsedRouteValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionAffinityProviders = sessionAffinityProviders?.ToProviderDictionary() ?? throw new ArgumentNullException(nameof(sessionAffinityProviders));
            _affinityFailurePolicies = affinityFailurePolicies?.ToPolicyDictionary() ?? throw new ArgumentNullException(nameof(affinityFailurePolicies));
        }

        public async Task<DynamicConfigRoot> BuildConfigAsync(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters, CancellationToken cancellation)
        {
            _ = routes ?? throw new ArgumentNullException(nameof(routes));
            _ = clusters ?? throw new ArgumentNullException(nameof(clusters));

            var configuredClusters = await GetClustersAsync(clusters, cancellation);
            var configuredRoutes = await GetRoutesAsync(routes, cancellation);

            var config = new DynamicConfigRoot
            {
                Clusters = configuredClusters,
                Routes = configuredRoutes,
            };

            return config;
        }

        public async Task<IDictionary<string, Cluster>> GetClustersAsync(IReadOnlyList<Cluster> clusters, CancellationToken cancellation)
        {
            var configuredClusters = new Dictionary<string, Cluster>(StringComparer.OrdinalIgnoreCase);
            // The IClustersRepo provides a fresh snapshot that we need to reconfigure each time.
            foreach (var c in clusters)
            {
                try
                {
                    // Don't modify the original
                    var cluster = c.DeepClone();

                    foreach (var filter in _filters)
                    {
                        await filter.ConfigureClusterAsync(cluster, cancellation);
                    }

                    ValidateSessionAffinity(cluster);

                    if (configuredClusters.ContainsKey(cluster.Id))
                    {
                        // TODO: Error
                    }

                    configuredClusters[cluster.Id] = cluster;
                }
                catch (Exception ex)
                {
                    Log.ClusterConfigException(_logger, c.Id, ex);
                }
            }

            return configuredClusters;
        }

        private void ValidateSessionAffinity(Cluster cluster)
        {
            if (cluster.SessionAffinity == null || !cluster.SessionAffinity.Enabled)
            {
                // Session affinity is disabled
                return;
            }

            if (string.IsNullOrEmpty(cluster.SessionAffinity.Mode))
            {
                cluster.SessionAffinity.Mode = SessionAffinityConstants.Modes.Cookie;
            }

            var affinityMode = cluster.SessionAffinity.Mode;
            if (!_sessionAffinityProviders.ContainsKey(affinityMode))
            {
                Log.NoSessionAffinityProviderFound(_logger, affinityMode, cluster.Id);
            }

            if (string.IsNullOrEmpty(cluster.SessionAffinity.FailurePolicy))
            {
                cluster.SessionAffinity.FailurePolicy = SessionAffinityConstants.AffinityFailurePolicies.Redistribute;
            }

            var affinityFailurePolicy = cluster.SessionAffinity.FailurePolicy;
            if (!_affinityFailurePolicies.ContainsKey(affinityFailurePolicy))
            {
                Log.NoAffinityFailurePolicyFound(_logger, affinityFailurePolicy, cluster.Id);
            }
        }

        private async Task<IList<ProxyRoute>> GetRoutesAsync(IReadOnlyList<ProxyRoute> routes, CancellationToken cancellation)
        {
            var seenRouteIds = new HashSet<string>();
            var sortedRoutes = new SortedList<(int, string), ProxyRoute>(routes?.Count ?? 0);
            if (routes == null)
            {
                return sortedRoutes.Values;
            }

            foreach (var r in routes)
            {
                if (seenRouteIds.Contains(r.RouteId))
                {
                    Log.DuplicateRouteId(_logger, r.RouteId);
                    continue;
                }

                // Don't modify the original
                var route = r.DeepClone();

                try
                {
                    foreach (var filter in _filters)
                    {
                        await filter.ConfigureRouteAsync(route, cancellation);
                    }
                }
                catch (Exception ex)
                {
                    Log.RouteConfigException(_logger, route.RouteId, ex);
                    continue;
                }

                if (!await _parsedRouteValidator.ValidateRouteAsync(route))
                {
                    // parsedRouteValidator already reported error message
                    continue;
                }

                sortedRoutes.Add((route.Priority ?? 0, route.RouteId), route);
            }

            return sortedRoutes.Values;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _clusterConfigException = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.ClusterConfigException,
                "An exception was thrown from the configuration callbacks for cluster `{clusterId}`.");

            private static readonly Action<ILogger, string, string, Exception> _noSessionAffinityProviderFound = LoggerMessage.Define<string, string>(
               LogLevel.Error,
               EventIds.NoSessionAffinityProviderFound,
               "No matching ISessionAffinityProvider found for the session affinity mode `{affinityMode}` set on the cluster `{clusterId}`.");

            private static readonly Action<ILogger, string, string, Exception> _noAffinityFailurePolicyFound = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.NoAffinityFailurePolicyFound,
                "No matching IAffinityFailurePolicy found for the affinity failure policy name {affinityFailurePolicy} set on the cluster {clusterId}.");

            private static readonly Action<ILogger, string, Exception> _routeConfigException = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.RouteConfigException,
                "An exception was thrown from the configuration callbacks for route `{routeId}`.");

            private static readonly Action<ILogger, string, Exception> _duplicateRouteId = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.DuplicateRouteId,
                "Duplicate route '{RouteId}'.");

            public static void ClusterConfigException(ILogger logger, string clusterId, Exception exception)
            {
                _clusterConfigException(logger, clusterId, exception);
            }

            public static void NoSessionAffinityProviderFound(ILogger logger, string affinityMode, string clusterId)
            {
                _noSessionAffinityProviderFound(logger, affinityMode, clusterId, null);
            }

            public static void NoAffinityFailurePolicyFound(ILogger logger, string affinityFailurePolicy, string clusterId)
            {
                _noAffinityFailurePolicyFound(logger, affinityFailurePolicy, clusterId, null);
            }

            public static void RouteConfigException(ILogger logger, string routeId, Exception exception)
            {
                _routeConfigException(logger, routeId, exception);
            }

            public static void DuplicateRouteId(ILogger logger, string routeId)
            {
                _duplicateRouteId(logger, routeId, null);
            }
        }
    }
}
