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
using System.Text;
using System.IO;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Routing;

internal sealed class ProxyEndpointFactory
{
    private static readonly IAuthorizeData _defaultAuthorization = new AuthorizeAttribute();
    private static readonly IEnableCorsAttribute _defaultCors = new EnableCorsAttribute();
    private static readonly IDisableCorsAttribute _disableCors = new DisableCorsAttribute();
    private static readonly IAllowAnonymous _allowAnonymous = new AllowAnonymousAttribute();
    private static readonly IContentTypeProvider _contentTypeProvider = new FileExtensionContentTypeProvider();

    private RequestDelegate? _pipeline;

    public Endpoint CreateEndpoint(RouteModel route, IReadOnlyList<Action<EndpointBuilder>> conventions)
    {
        var config = route.Config;
        var match = config.Match;

        // Catch-all pattern when no path was specified
        var pathPattern = string.IsNullOrEmpty(match.Path) ? "/{**catchall}" : match.Path;
        RequestDelegate? requestDelegate;
        if (route.Config.Response != null)
        {
            requestDelegate = CreateResponseDelegate(route);
        }
        else if (route.Config.Files?.Root != null)
        {
            requestDelegate = CreateStaticFilesDelegate(route);
        }
        else
        {
            requestDelegate = _pipeline ?? throw new InvalidOperationException("The pipeline hasn't been provided yet.");
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

            if (!string.IsNullOrEmpty(routeResponse.BodyText))
            {
                response.ContentLength = Encoding.UTF8.GetByteCount(routeResponse.BodyText);
                await response.WriteAsync(routeResponse.BodyText);
            }
            else if (!string.IsNullOrEmpty(routeResponse.BodyFilePath))
            {
                // TODO: if File contains a pattern, get the real path from the route values.

                var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var file = env.WebRootFileProvider.GetFileInfo(routeResponse.BodyFilePath);
                if (file == null)
                {
                    throw new FileNotFoundException("Unable to find the BodyFilePath.", routeResponse.BodyFilePath);
                }

                // Would you ever want to disable this?
                if (string.IsNullOrEmpty(response.ContentType)
                    && _contentTypeProvider.TryGetContentType(routeResponse.BodyFilePath, out var contentType))
                {
                    response.ContentType = contentType;
                }
                // TODO: Default content-type if not specified?
                // Not supported: ranges, e-tags, last-modified, etc.
                // the client isn't supposed to know this is a file.
                response.ContentLength = file.Length;
                await response.SendFileAsync(file);
            }

            await route.Transformer.TransformResponseTrailersAsync(context, null);
        };
    }

    private static RequestDelegate CreateStaticFilesDelegate(RouteModel route)
    {
        var options = new FileServerOptions();
        // TODO: https://github.com/dotnet/aspnetcore/pull/45062 .NET 8 OnPrepareResponseAsync
        options.StaticFileOptions.OnPrepareResponse = context =>
        {
            context.Context.Response.OnStarting(state =>
            {
                var httpContext = (HttpContext)state;
                return route.Transformer.TransformResponseAsync(httpContext, null).AsTask();
            }, context.Context);
        };
        // TODO: Content-Type mappings
        // TODO: ServeUnknownFileTypes, default content-type
        // TODO: Default files
        // TODO: Directory browsing

        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        builder.Use(async (context, next) =>
        {
            // Clear the endpoint so static files will run.
            context.SetEndpoint(null);
            await next(context);
            await route.Transformer.TransformResponseTrailersAsync(context, null);
        });
        builder.UseFileServer(options);
        return builder.Build();
    }

    public void SetProxyPipeline(RequestDelegate pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }
}
