// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service
{
    internal class DynamicConfigBuilder : IDynamicConfigBuilder
    {
        private readonly IEnumerable<IProxyConfigFilter> _filters;
        private readonly IClustersRepo _clustersRepo;
        private readonly IRoutesRepo _routesRepo;
        private readonly IRouteValidator _parsedRouteValidator;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;
        private readonly IDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;

        public DynamicConfigBuilder(
            IEnumerable<IProxyConfigFilter> filters,
            IClustersRepo clustersRepo,
            IRoutesRepo routesRepo,
            IRouteValidator parsedRouteValidator,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies)
        {
            _filters = filters ?? throw new ArgumentNullException(nameof(filters));
            _clustersRepo = clustersRepo ?? throw new ArgumentNullException(nameof(clustersRepo));
            _routesRepo = routesRepo ?? throw new ArgumentNullException(nameof(routesRepo));
            _parsedRouteValidator = parsedRouteValidator ?? throw new ArgumentNullException(nameof(parsedRouteValidator));
            _sessionAffinityProviders = sessionAffinityProviders?.ToProviderDictionary() ?? throw new ArgumentNullException(nameof(sessionAffinityProviders));
            _affinityFailurePolicies = affinityFailurePolicies?.ToPolicyDictionary() ?? throw new ArgumentNullException(nameof(affinityFailurePolicies));
        }

        public async Task<DynamicConfigRoot> BuildConfigAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            var clusters = await GetClustersAsync(errorReporter, cancellation);
            var routes = await GetRoutesAsync(errorReporter, cancellation);

            var config = new DynamicConfigRoot
            {
                Clusters = clusters,
                Routes = routes,
            };

            return config;
        }

        public async Task<IDictionary<string, Cluster>> GetClustersAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            var clusters = await _clustersRepo.GetClustersAsync(cancellation) ?? new Dictionary<string, Cluster>(StringComparer.Ordinal);
            var configuredClusters = new Dictionary<string, Cluster>(StringComparer.Ordinal);
            // The IClustersRepo provides a fresh snapshot that we need to reconfigure each time.
            foreach (var (id, cluster) in clusters)
            {
                try
                {
                    if (id != cluster.Id)
                    {
                        errorReporter.ReportError(ConfigErrors.ConfigBuilderClusterIdMismatch, id,
                            $"The cluster Id '{cluster.Id}' and its lookup key '{id}' do not match.");
                        continue;
                    }

                    foreach (var filter in _filters)
                    {
                        await filter.ConfigureClusterAsync(cluster, cancellation);
                    }

                    ValidateSessionAffinity(errorReporter, id, cluster);

                    configuredClusters[id] = cluster;
                }
                catch (Exception ex)
                {
                    errorReporter.ReportError(ConfigErrors.ConfigBuilderClusterException, id, "An exception was thrown from the configuration callbacks.", ex);
                }
            }

            return configuredClusters;
        }

        private void ValidateSessionAffinity(IConfigErrorReporter errorReporter, string id, Cluster cluster)
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
                errorReporter.ReportError(ConfigErrors.ConfigBuilderClusterNoProviderFoundForSessionAffinityMode, id, $"No matching {nameof(ISessionAffinityProvider)} found for the session affinity mode {affinityMode} set on the cluster {cluster.Id}.");
            }

            if (string.IsNullOrEmpty(cluster.SessionAffinity.FailurePolicy))
            {
                cluster.SessionAffinity.FailurePolicy = SessionAffinityConstants.AffinityFailurePolicies.Redistribute;
            }

            var affinityFailurePolicy = cluster.SessionAffinity.FailurePolicy;
            if (!_affinityFailurePolicies.ContainsKey(affinityFailurePolicy))
            {
                errorReporter.ReportError(ConfigErrors.ConfigBuilderClusterNoAffinityFailurePolicyFoundForSpecifiedName, id, $"No matching {nameof(IAffinityFailurePolicy)} found for the affinity failure policy name {affinityFailurePolicy} set on the cluster {cluster.Id}.");
            }
        }

        private async Task<IList<ParsedRoute>> GetRoutesAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            var routes = await _routesRepo.GetRoutesAsync(cancellation);

            var seenRouteIds = new HashSet<string>();
            var sortedRoutes = new SortedList<(int, string), ParsedRoute>(routes?.Count ?? 0);
            if (routes == null)
            {
                return sortedRoutes.Values;
            }

            foreach (var route in routes)
            {
                if (seenRouteIds.Contains(route.RouteId))
                {
                    errorReporter.ReportError(ConfigErrors.RouteDuplicateId, route.RouteId, $"Duplicate route '{route.RouteId}'.");
                    continue;
                }

                try
                {
                    foreach (var filter in _filters)
                    {
                        await filter.ConfigureRouteAsync(route, cancellation);
                    }
                }
                catch (Exception ex)
                {
                    errorReporter.ReportError(ConfigErrors.ConfigBuilderClusterException, route.RouteId, "An exception was thrown from the configuration callbacks.", ex);
                    continue;
                }

                var parsedRoute = new ParsedRoute
                {
                    RouteId = route.RouteId,
                    Methods = route.Match.Methods,
                    Host = route.Match.Host,
                    Path = route.Match.Path,
                    Priority = route.Priority,
                    ClusterId = route.ClusterId,
                    AuthorizationPolicy = route.AuthorizationPolicy,
                    Metadata = route.Metadata,
                    Transforms = route.Transforms,
                };

                if (!_parsedRouteValidator.ValidateRoute(parsedRoute, errorReporter))
                {
                    // parsedRouteValidator already reported error message
                    continue;
                }

                sortedRoutes.Add((parsedRoute.Priority ?? 0, parsedRoute.RouteId), parsedRoute);
            }

            return sortedRoutes.Values;
        }
    }
}
