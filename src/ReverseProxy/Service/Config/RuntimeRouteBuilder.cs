// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.RouteDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Config;
using CorsConstants = Microsoft.ReverseProxy.Abstractions.RouteDiscovery.Contract.CorsConstants;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// Default implementation of the <see cref="IRuntimeRouteBuilder"/> interface.
    /// </summary>
    internal class RuntimeRouteBuilder : IRuntimeRouteBuilder
    {
        private static readonly IAuthorizeData DefaultAuthorization = new AuthorizeAttribute();
        private static readonly IEnableCorsAttribute DefaultCors = new EnableCorsAttribute();
        private static readonly IDisableCorsAttribute DisableCors = new DisableCorsAttribute();

        private readonly ITransformBuilder _transformBuilder;
        private RequestDelegate _pipeline;

        public RuntimeRouteBuilder(ITransformBuilder transformBuilder)
        {
            _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
        }

        public void SetProxyPipeline(RequestDelegate pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        /// <inheritdoc/>
        public RouteConfig Build(ProxyRoute source, ClusterInfo cluster, RouteInfo runtimeRoute)
        {
            _ = source ?? throw new ArgumentNullException(nameof(source));
            _ = runtimeRoute ?? throw new ArgumentNullException(nameof(runtimeRoute));

            var transforms = _transformBuilder.Build(source.Transforms);

            // NOTE: `new RouteConfig(...)` needs a reference to the list of ASP .NET Core endpoints,
            // but the ASP .NET Core endpoints cannot be created without a `RouteConfig` metadata item.
            // We solve this chicken-egg problem by creating an (empty) list first
            // and passing a read-only wrapper of it to `RouteConfig.ctor`.
            // Recall that `List<T>.AsReadOnly()` creates a wrapper over the original list,
            // and changes to the underlying list *are* reflected on the read-only view.
            var aspNetCoreEndpoints = new List<Endpoint>(1);
            var newRouteConfig = new RouteConfig(
                runtimeRoute,
                source,
                cluster,
                aspNetCoreEndpoints.AsReadOnly(),
                transforms);

            // Catch-all pattern when no path was specified
            var pathPattern = string.IsNullOrEmpty(source.Match.Path) ? "/{**catchall}" : source.Match.Path;

            var endpointBuilder = new AspNetCore.Routing.RouteEndpointBuilder(
                requestDelegate: _pipeline,
                routePattern: AspNetCore.Routing.Patterns.RoutePatternFactory.Parse(pathPattern),
                order: source.Order.GetValueOrDefault());

            endpointBuilder.DisplayName = source.RouteId;
            endpointBuilder.Metadata.Add(newRouteConfig);

            if (source.Match.Hosts != null && source.Match.Hosts.Count != 0)
            {
                endpointBuilder.Metadata.Add(new AspNetCore.Routing.HostAttribute(source.Match.Hosts.ToArray()));
            }

            bool acceptCorsPreflight;
            if (string.Equals(CorsConstants.Default, source.CorsPolicy, StringComparison.OrdinalIgnoreCase))
            {
                endpointBuilder.Metadata.Add(DefaultCors);
                acceptCorsPreflight = true;
            }
            else if (string.Equals(CorsConstants.Disable, source.CorsPolicy, StringComparison.OrdinalIgnoreCase))
            {
                endpointBuilder.Metadata.Add(DisableCors);
                acceptCorsPreflight = true;
            }
            else if (!string.IsNullOrEmpty(source.CorsPolicy))
            {
                endpointBuilder.Metadata.Add(new EnableCorsAttribute(source.CorsPolicy));
                acceptCorsPreflight = true;
            }
            else
            {
                acceptCorsPreflight = false;
            }

            if (source.Match.Methods != null && source.Match.Methods.Count > 0)
            {
                endpointBuilder.Metadata.Add(new AspNetCore.Routing.HttpMethodMetadata(source.Match.Methods, acceptCorsPreflight));
            }

            if (string.Equals(AuthorizationConstants.Default, source.AuthorizationPolicy, StringComparison.OrdinalIgnoreCase))
            {
                endpointBuilder.Metadata.Add(DefaultAuthorization);
            }
            else if (!string.IsNullOrEmpty(source.AuthorizationPolicy))
            {
                endpointBuilder.Metadata.Add(new AuthorizeAttribute(source.AuthorizationPolicy));
            }

            var endpoint = endpointBuilder.Build();
            aspNetCoreEndpoints.Add(endpoint);

            return newRouteConfig;
        }
    }
}
