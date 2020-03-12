// <copyright file="DynamicConfigBuilder.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal class DynamicConfigBuilder : IDynamicConfigBuilder
    {
        private readonly IBackendsRepo backendsRepo;
        private readonly IBackendEndpointsRepo endpointsRepo;
        private readonly IRoutesRepo routesRepo;
        private readonly IRouteParser routeParser;
        private readonly IRouteValidator parsedRouteValidator;

        public DynamicConfigBuilder(
            IBackendsRepo backendsRepo,
            IBackendEndpointsRepo endpointsRepo,
            IRoutesRepo routesRepo,
            IRouteParser routeParser,
            IRouteValidator parsedRouteValidator)
        {
            Contracts.CheckValue(backendsRepo, nameof(backendsRepo));
            Contracts.CheckValue(endpointsRepo, nameof(endpointsRepo));
            Contracts.CheckValue(routesRepo, nameof(routesRepo));
            Contracts.CheckValue(routeParser, nameof(routeParser));
            Contracts.CheckValue(parsedRouteValidator, nameof(parsedRouteValidator));

            this.backendsRepo = backendsRepo;
            this.endpointsRepo = endpointsRepo;
            this.routesRepo = routesRepo;
            this.routeParser = routeParser;
            this.parsedRouteValidator = parsedRouteValidator;
        }

        public async Task<Result<DynamicConfigRoot>> BuildConfigAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            Contracts.CheckValue(errorReporter, nameof(errorReporter));

            var backends = await this.GetBackendsWithEndpointsAsync(errorReporter, cancellation);
            var routes = await this.GetRoutesAsync(errorReporter, cancellation);

            var config = new DynamicConfigRoot
            {
                Backends = backends,
                Routes = routes,
            };

            return Result.Success(config);
        }

        private async Task<IList<BackendWithEndpoints>> GetBackendsWithEndpointsAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            var backends = await this.backendsRepo.GetBackendsAsync(cancellation);
            var sortedBackends = new SortedList<string, BackendWithEndpoints>(backends?.Count ?? 0, StringComparer.Ordinal);
            if (backends != null)
            {
                foreach (var backend in backends)
                {
                    if (sortedBackends.ContainsKey(backend.BackendId))
                    {
                        errorReporter.ReportError(ConfigErrors.BackendDuplicateId, backend.BackendId, $"Duplicate backend '{backend.BackendId}'.");
                        continue;
                    }

                    var endpoints = await this.GetBackendEndpointsAsync(errorReporter, backend.BackendId, cancellation);
                    sortedBackends.Add(
                        backend.BackendId,
                        new BackendWithEndpoints(
                            backend: backend.DeepClone(),
                            endpoints: endpoints)); // `this.GetBackendEndpoints` already returns deep-cloned copies
                }
            }

            return sortedBackends.Values;
        }

        private async Task<IList<BackendEndpoint>> GetBackendEndpointsAsync(IConfigErrorReporter errorReporter, string backendId, CancellationToken cancellation)
        {
            var endpoints = await this.endpointsRepo.GetEndpointsAsync(backendId, cancellation);
            var backendEndpoints = new SortedList<string, BackendEndpoint>(endpoints?.Count ?? 0, StringComparer.Ordinal);
            if (endpoints != null)
            {
                foreach (var endpoint in endpoints)
                {
                    if (backendEndpoints.ContainsKey(endpoint.EndpointId))
                    {
                        errorReporter.ReportError(ConfigErrors.BackendEndpointDuplicateId, endpoint.EndpointId, $"Duplicate endpoint '{endpoint.EndpointId}'.");
                        continue;
                    }

                    backendEndpoints.Add(endpoint.EndpointId, endpoint.DeepClone());
                }
            }

            return backendEndpoints.Values;
        }

        private async Task<IList<ParsedRoute>> GetRoutesAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            var routes = await this.routesRepo.GetRoutesAsync(cancellation);

            var seenRouteIds = new HashSet<string>();
            var sortedRoutes = new SortedList<(int, string), ParsedRoute>(routes?.Count ?? 0);
            if (routes != null)
            {
                foreach (var route in routes)
                {
                    if (seenRouteIds.Contains(route.RouteId))
                    {
                        errorReporter.ReportError(ConfigErrors.RouteDuplicateId, route.RouteId, $"Duplicate route '{route.RouteId}'.");
                        continue;
                    }

                    var parsedResult = this.routeParser.ParseRoute(route, errorReporter);
                    if (!parsedResult.IsSuccess)
                    {
                        // routeParser already reported error message
                        continue;
                    }

                    var parsedRoute = parsedResult.Value;
                    if (!this.parsedRouteValidator.ValidateRoute(parsedRoute, errorReporter))
                    {
                        // parsedRouteValidator already reported error message
                        continue;
                    }

                    sortedRoutes.Add((parsedRoute.Priority ?? 0, parsedRoute.RouteId), parsedRoute);
                }
            }

            return sortedRoutes.Values;
        }
    }
}
