// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Proxy;
using IslandGateway.Utilities;
using AspNetCore = Microsoft.AspNetCore;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Default implementation of the <see cref="IRuntimeRouteBuilder"/> interface.
    /// </summary>
    internal class RuntimeRouteBuilder : IRuntimeRouteBuilder
    {
        private readonly IProxyInvoker _proxyInvoker;

        public RuntimeRouteBuilder(IProxyInvoker proxyInvoker)
        {
            Contracts.CheckValue(proxyInvoker, nameof(proxyInvoker));
            _proxyInvoker = proxyInvoker;
        }

        /// <inheritdoc/>
        public RouteConfig Build(ParsedRoute source, BackendInfo backendOrNull, RouteInfo runtimeRoute)
        {
            Contracts.CheckValue(source, nameof(source));
            Contracts.CheckValue(runtimeRoute, nameof(runtimeRoute));

            // NOTE: `new RouteConfig(...)` needs a reference to the list of ASP .NET Core endpoints,
            // but the ASP .NET Core endpoints cannot be created without a `RouteConfig` metadata item.
            // We solve this chicken-egg problem by creating an (empty) list first
            // and passing a read-only wrapper of it to `RouteConfig.ctor`.
            // Recall that `List<T>.AsReadOnly()` creates a wrapper over the original list,
            // and changes to the underlying list *are* reflected on the read-only view.
            var aspNetCoreEndpoints = new List<AspNetCore.Http.Endpoint>(1);
            var newRouteConfig = new RouteConfig(
                route: runtimeRoute,
                matcherSummary: source.GetMatcherSummary(),
                priority: source.Priority,
                backendOrNull: backendOrNull,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly());

            // TODO: Handle arbitrary AST's properly
            // Catch-all pattern when no path was specified
            var pathPattern = string.IsNullOrEmpty(source.Path) ? "/{**catchall}" : source.Path;

            // TODO: Propagate route priority
            var endpointBuilder = new AspNetCore.Routing.RouteEndpointBuilder(
                requestDelegate: _proxyInvoker.InvokeAsync,
                routePattern: AspNetCore.Routing.Patterns.RoutePatternFactory.Parse(pathPattern),
                order: 0);
            endpointBuilder.DisplayName = source.RouteId;
            endpointBuilder.Metadata.Add(newRouteConfig);

            if (source.Host != null)
            {
                endpointBuilder.Metadata.Add(new AspNetCore.Routing.HostAttribute(source.Host));
            }

            var endpoint = endpointBuilder.Build();
            aspNetCoreEndpoints.Add(endpoint);

            return newRouteConfig;
        }
    }
}
