// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Yarp.ReverseProxy.Model;
using CorsConstants = Yarp.ReverseProxy.Configuration.CorsConstants;
using AuthorizationConstants = Yarp.ReverseProxy.Configuration.AuthorizationConstants;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Routing;

internal sealed class ProxyEndpointFactory
{
    private static readonly IAuthorizeData _defaultAuthorization = new AuthorizeAttribute();
    private static readonly IEnableCorsAttribute _defaultCors = new EnableCorsAttribute();
    private static readonly IDisableCorsAttribute _disableCors = new DisableCorsAttribute();
    private static readonly IAllowAnonymous _allowAnonymous = new AllowAnonymousAttribute();

    private RequestDelegate? _pipeline;

    public Endpoint CreateEndpoint(RouteModel route, IReadOnlyList<Action<EndpointBuilder>> conventions)
    {
        var config = route.Config;
        var match = config.Match;

        // Catch-all pattern when no path was specified
        var pathPattern = string.IsNullOrEmpty(match.Path) ? "/{**catchall}" : match.Path;
        var requestDelegate = _pipeline ?? throw new InvalidOperationException("The pipeline hasn't been provided yet.");
        if (route.Config.Response != null)
        {
            requestDelegate = CreateResponseDelegate(route);
        }

        var endpointBuilder = new RouteEndpointBuilder(
            requestDelegate: requestDelegate,
            routePattern: RoutePatternFactory.Parse(pathPattern),
            order: config.Order.GetValueOrDefault())
        {
            DisplayName = config.RouteId
        };

        endpointBuilder.Metadata.Add(route);

        if (match.Hosts is not null && match.Hosts.Count != 0)
        {
            endpointBuilder.Metadata.Add(new HostAttribute(match.Hosts.ToArray()));
        }

        if (match.Headers is not null && match.Headers.Count > 0)
        {
            var matchers = new List<HeaderMatcher>(match.Headers.Count);
            foreach (var header in match.Headers)
            {
                matchers.Add(new HeaderMatcher(header.Name, header.Values, header.Mode, header.IsCaseSensitive));
            }

            endpointBuilder.Metadata.Add(new HeaderMetadata(matchers));
        }

        if (match.QueryParameters is not null && match.QueryParameters.Count > 0)
        {
            var matchers = new List<QueryParameterMatcher>(match.QueryParameters.Count);
            foreach (var queryparam in match.QueryParameters)
            {
                matchers.Add(new QueryParameterMatcher(queryparam.Name, queryparam.Values, queryparam.Mode, queryparam.IsCaseSensitive));
            }

            endpointBuilder.Metadata.Add(new QueryParameterMetadata(matchers));
        }

        bool acceptCorsPreflight;
        if (string.Equals(CorsConstants.Default, config.CorsPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_defaultCors);
            acceptCorsPreflight = true;
        }
        else if (string.Equals(CorsConstants.Disable, config.CorsPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_disableCors);
            acceptCorsPreflight = true;
        }
        else if (!string.IsNullOrEmpty(config.CorsPolicy))
        {
            endpointBuilder.Metadata.Add(new EnableCorsAttribute(config.CorsPolicy));
            acceptCorsPreflight = true;
        }
        else
        {
            acceptCorsPreflight = false;
        }

        if (match.Methods is not null && match.Methods.Count > 0)
        {
            endpointBuilder.Metadata.Add(new HttpMethodMetadata(match.Methods, acceptCorsPreflight));
        }

        if (string.Equals(AuthorizationConstants.Default, config.AuthorizationPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_defaultAuthorization);
        }
        else if (string.Equals(AuthorizationConstants.Anonymous, config.AuthorizationPolicy, StringComparison.OrdinalIgnoreCase))
        {
            endpointBuilder.Metadata.Add(_allowAnonymous);
        }
        else if (!string.IsNullOrEmpty(config.AuthorizationPolicy))
        {
            endpointBuilder.Metadata.Add(new AuthorizeAttribute(config.AuthorizationPolicy));
        }

        for (var i = 0; i < conventions.Count; i++)
        {
            conventions[i](endpointBuilder);
        }

        return endpointBuilder.Build();
    }

    private static RequestDelegate CreateResponseDelegate(RouteModel route)
    {
        return async context =>
        {
            var response = context.Response;
            var routeResponse = route.Config.Response!;
            if (routeResponse.StatusCode.HasValue)
            {
                response.StatusCode = routeResponse.StatusCode.Value;
            }
            if (!string.IsNullOrEmpty(routeResponse.ContentType))
            {
                response.ContentType = routeResponse.ContentType;
            }

            await route.Transformer.TransformResponseAsync(context, null);

            if (!string.IsNullOrEmpty(routeResponse.Body))
            {
                await response.WriteAsync(routeResponse.Body);
            }
            else if (!string.IsNullOrEmpty(routeResponse.File))
            {
                // TODO: if File contains a pattern, get the real path from the route values.

                var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var file = env.WebRootFileProvider.GetFileInfo(routeResponse.File);
                // TODO: Default content-type
                // TODO: Content-Length, etc.
                // TODO: UseFileServer?
                if (file != null)
                {
                    await response.SendFileAsync(file);
                }
            }

            await route.Transformer.TransformResponseTrailersAsync(context, null);
        };
    }

    public void SetProxyPipeline(RequestDelegate pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }
}
