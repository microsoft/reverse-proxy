// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for <see cref="IEndpointRouteBuilder"/> used to add direct forwarding to the ASP.NET Core request pipeline.
/// </summary>
public static class DirectForwardingIEndpointRouteBuilderExtensions
{
    private static HttpMessageInvoker? _defaultTransport;

    private static HttpMessageInvoker DefaultTransport
    {
        get
        {
            _defaultTransport ??= new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current)
            });

            return _defaultTransport;
        }
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using default configuration for the outgoing request, default transforms, and default HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string serviceUrl)
    {
        return endpoints.MapForwarder(pattern, serviceUrl, ForwarderRequestConfig.Empty, HttpTransformer.Default, DefaultTransport);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using customized configuration for the outgoing request, default transforms, and default HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string serviceUrl, ForwarderRequestConfig requestConfig)
    {
        return endpoints.MapForwarder(pattern, serviceUrl, requestConfig, HttpTransformer.Default, DefaultTransport);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using customized configuration for the outgoing request, customized transforms, and default HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string serviceUrl, ForwarderRequestConfig requestConfig, HttpTransformer transforms)
    {
        return endpoints.MapForwarder(pattern, serviceUrl, requestConfig, transforms, DefaultTransport);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using customized configuration for the outgoing request, customized transforms, and customized HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string serviceUrl, ForwarderRequestConfig requestConfig, HttpTransformer transforms, HttpMessageInvoker transport)
    {
        var forwarder = endpoints.ServiceProvider.GetRequiredService<IHttpForwarder>();

        return endpoints.Map(pattern, async httpContext =>
        {
            var error = await forwarder.SendAsync(httpContext, serviceUrl, transport, requestConfig, transforms);

            if (error != ForwarderError.None)
            {
                var errorFeature = httpContext.GetForwarderErrorFeature();
                throw new Exception("An error has occurred while forwarding the request", errorFeature?.Exception);
            }
        });
    }
}
