// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service
{
    internal class DynamicConfigBuilder : IDynamicConfigBuilder
    {
        private readonly IEnumerable<IProxyConfigFilter> _filters;
        private readonly IBackendsRepo _backendsRepo;
        private readonly IRoutesRepo _routesRepo;
        private readonly IRouteValidator _parsedRouteValidator;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;
        private readonly IDictionary<string, IMissingDestinationHandler> _missingDestionationHandlers;

        public DynamicConfigBuilder(
            IEnumerable<IProxyConfigFilter> filters,
            IBackendsRepo backendsRepo,
            IRoutesRepo routesRepo,
            IRouteValidator parsedRouteValidator,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IEnumerable<IMissingDestinationHandler> missingDestinationHandlers)
        {
            Contracts.CheckValue(filters, nameof(filters));
            Contracts.CheckValue(backendsRepo, nameof(backendsRepo));
            Contracts.CheckValue(routesRepo, nameof(routesRepo));
            Contracts.CheckValue(parsedRouteValidator, nameof(parsedRouteValidator));
            Contracts.CheckValue(sessionAffinityProviders, nameof(sessionAffinityProviders));
            Contracts.CheckValue(missingDestinationHandlers, nameof(missingDestinationHandlers));
            _filters = filters;
            _backendsRepo = backendsRepo;
            _routesRepo = routesRepo;
            _parsedRouteValidator = parsedRouteValidator;
            _sessionAffinityProviders = sessionAffinityProviders.ToProviderDictionary();
            _missingDestionationHandlers = missingDestinationHandlers.ToHandlerDictionary();
        }

        public async Task<Result<DynamicConfigRoot>> BuildConfigAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            Contracts.CheckValue(errorReporter, nameof(errorReporter));

            var backends = await GetBackendsAsync(errorReporter, cancellation);
            var routes = await GetRoutesAsync(errorReporter, cancellation);

            var config = new DynamicConfigRoot
            {
                Backends = backends,
                Routes = routes,
            };

            return Result.Success(config);
        }

        public async Task<IDictionary<string, Backend>> GetBackendsAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation)
        {
            var backends = await _backendsRepo.GetBackendsAsync(cancellation) ?? new Dictionary<string, Backend>(StringComparer.Ordinal);
            var configuredBackends = new Dictionary<string, Backend>(StringComparer.Ordinal);
            // The IBackendsRepo provides a fresh snapshot that we need to reconfigure each time.
            foreach (var (id, backend) in backends)
            {
                try
                {
                    if (id != backend.Id)
                    {
                        errorReporter.ReportError(ConfigErrors.ConfigBuilderBackendIdMismatch, id,
                            $"The backend Id '{backend.Id}' and its lookup key '{id}' do not match.");
                        continue;
                    }

                    ValidateSessionAffinity(errorReporter, id, backend);

                    foreach (var filter in _filters)
                    {
                        await filter.ConfigureBackendAsync(backend, cancellation);
                    }

                    configuredBackends[id] = backend;
                }
                catch (Exception ex)
                {
                    errorReporter.ReportError(ConfigErrors.ConfigBuilderBackendException, id, "An exception was thrown from the configuration callbacks.", ex);
                }
            }

            return configuredBackends;
        }

        private void ValidateSessionAffinity(IConfigErrorReporter errorReporter, string id, Backend backend)
        {
            if (backend.SessionAffinity == null) // Session affinity is disabled
            {
                return;
            }

            var affinityMode = backend.SessionAffinity.Mode;
            if (affinityMode == null)
            {
                errorReporter.ReportError(ConfigErrors.ConfigBuilderBackendSessionAffinityModeIsNull, id, $"The session affinity mode is null for the backend {backend.Id}.");
            }
            else if (!_sessionAffinityProviders.ContainsKey(affinityMode))
            {
                errorReporter.ReportError(ConfigErrors.ConfigBuilderBackendNoProviderFoundForSessionAffinityMode, id, $"No matching {nameof(ISessionAffinityProvider)} found for the session affinity mode {affinityMode} set on the backend {backend.Id}.");
            }

            var missingDestinationHandler = backend.SessionAffinity.MissingDestinationHandler;
            if (missingDestinationHandler == null)
            {
                errorReporter.ReportError(ConfigErrors.ConfigBuilderBackendMissingDestinationHandlerIsNull, id, $"The missing affinitizated destination handler name is null for the backend {backend.Id}.");
            }
            else if (!_missingDestionationHandlers.ContainsKey(missingDestinationHandler))
            {
                errorReporter.ReportError(ConfigErrors.ConfigBuilderBackendNoMissingDestinationHandlerFoundForSpecifiedName, id, $"No matching {nameof(IMissingDestinationHandler)} found for the missing affinitizated destination handler name {missingDestinationHandler} set on the backend {backend.Id}.");
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
                    errorReporter.ReportError(ConfigErrors.ConfigBuilderBackendException, route.RouteId, "An exception was thrown from the configuration callbacks.", ex);
                    continue;
                }

                var parsedRoute = new ParsedRoute {
                    RouteId = route.RouteId,
                    Methods = route.Match.Methods,
                    Host = route.Match.Host,
                    Path = route.Match.Path,
                    Priority = route.Priority,
                    BackendId = route.BackendId,
                    Metadata = route.Metadata,
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
