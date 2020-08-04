// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;

namespace Microsoft.ReverseProxy.Service
{
    internal class DynamicConfigBuilder : IDynamicConfigBuilder
    {
        private readonly IEnumerable<IProxyConfigFilter> _filters;
        private readonly IConfigValidator _configValidator;

        public DynamicConfigBuilder(
            IEnumerable<IProxyConfigFilter> filters,
            IConfigValidator configValidator)
        {
            _filters = filters ?? throw new ArgumentNullException(nameof(filters));
            _configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator));
        }

        public async Task<DynamicConfigRoot> BuildConfigAsync(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters, CancellationToken cancellation)
        {
            _ = routes ?? throw new ArgumentNullException(nameof(routes));
            _ = clusters ?? throw new ArgumentNullException(nameof(clusters));

            var (configuredRoutes, routeErrors) = await GetRoutesAsync(routes, cancellation);
            var (configuredClusters, clusterErrors) = await GetClustersAsync(clusters, cancellation);

            if (routeErrors.Count > 0 || clusterErrors.Count > 0)
            {
                throw new AggregateException("The proxy config is invalid.", routeErrors.Concat(clusterErrors));
            }

            var config = new DynamicConfigRoot
            {
                Clusters = configuredClusters,
                Routes = configuredRoutes,
            };

            return config;
        }

        private async Task<(IDictionary<string, Cluster>, IList<Exception>)> GetClustersAsync(IReadOnlyList<Cluster> clusters, CancellationToken cancellation)
        {
            var configuredClusters = new Dictionary<string, Cluster>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<Exception>();
            // The IClustersRepo provides a fresh snapshot that we need to reconfigure each time.
            foreach (var c in clusters)
            {
                try
                {
                    if (configuredClusters.ContainsKey(c.Id))
                    {
                        errors.Add(new ArgumentException($"Duplicate cluster '{c.Id}'."));
                        continue;
                    }

                    // Don't modify the original
                    var cluster = c.DeepClone();

                    foreach (var filter in _filters)
                    {
                        await filter.ConfigureClusterAsync(cluster, cancellation);
                    }

                    var clusterErrors = await _configValidator.ValidateClusterAsync(cluster);
                    if (clusterErrors.Count > 0)
                    {
                        errors.AddRange(clusterErrors);
                        continue;
                    }

                    configuredClusters[cluster.Id] = cluster;
                }
                catch (Exception ex)
                {
                    errors.Add(new ArgumentException($"An exception was thrown from the configuration callbacks for cluster `{c.Id}`.", ex));
                }
            }

            return (configuredClusters, errors);
        }

        private async Task<(IList<ProxyRoute>, IList<Exception>)> GetRoutesAsync(IReadOnlyList<ProxyRoute> routes, CancellationToken cancellation)
        {
            var seenRouteIds = new HashSet<string>();
            var sortedRoutes = new SortedList<(int, string), ProxyRoute>(routes?.Count ?? 0);
            var errors = new List<Exception>();
            if (routes == null)
            {
                return (sortedRoutes.Values, errors);
            }

            foreach (var r in routes)
            {
                if (seenRouteIds.Contains(r.RouteId))
                {
                    errors.Add(new ArgumentException($"Duplicate route {r.RouteId}"));
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
                    errors.Add(new Exception($"An exception was thrown from the configuration callbacks for route `{r.RouteId}`.", ex));
                    continue;
                }

                var routeErrors = await _configValidator.ValidateRouteAsync(route);
                if (routeErrors.Count > 0)
                {
                    errors.AddRange(routeErrors);
                    continue;
                }

                sortedRoutes.Add((route.Priority ?? 0, route.RouteId), route);
            }

            if (errors.Count > 0)
            {
                return (null, errors);
            }

            return (sortedRoutes.Values, errors);
        }
    }
}
