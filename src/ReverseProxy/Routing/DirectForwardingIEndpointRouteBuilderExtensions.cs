// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Configuration;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for <see cref="IEndpointRouteBuilder"/> used to add direct forwarding to the ASP.NET Core request pipeline.
/// </summary>
public static class DirectForwardingIEndpointRouteBuilderExtensions
{
    private static readonly ConditionalWeakTable<IServiceProvider, HttpMessageInvoker> _httpClients = new();

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using default configuration for the outgoing request, default transforms, and default HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string destinationPrefix)
    {
        return endpoints.MapForwarder(pattern, destinationPrefix, ForwarderRequestConfig.Empty);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using customized configuration for the outgoing request, default transforms, and default HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string destinationPrefix, ForwarderRequestConfig requestConfig)
    {
        return endpoints.MapForwarder(pattern, destinationPrefix, requestConfig, HttpTransformer.Default);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using customized configuration for the outgoing request, customized transforms, and default HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string destinationPrefix, ForwarderRequestConfig requestConfig, HttpTransformer transformer)
    {
        var httpClient = GetHttpClient(endpoints.ServiceProvider);

        return endpoints.MapForwarder(pattern, destinationPrefix, requestConfig, transformer, httpClient);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using customized configuration for the outgoing request, customized transforms, and customized HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string destinationPrefix, ForwarderRequestConfig requestConfig, HttpTransformer transformer, HttpMessageInvoker httpClient)
    {
        ArgumentNullException.ThrowIfNull(endpoints, nameof(endpoints));
        ArgumentNullException.ThrowIfNull(destinationPrefix, nameof(destinationPrefix));
        ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));
        ArgumentNullException.ThrowIfNull(requestConfig, nameof(requestConfig));
        ArgumentNullException.ThrowIfNull(transformer, nameof(transformer));

        var forwarder = endpoints.ServiceProvider.GetRequiredService<IHttpForwarder>();

        return endpoints.Map(pattern, async httpContext =>
        {
            await forwarder.SendAsync(httpContext, destinationPrefix, httpClient, requestConfig, transformer);
        });
    }

    private static HttpMessageInvoker GetHttpClient(IServiceProvider serviceProvider)
    {
        lock (_httpClients)
        {
            return _httpClients.GetValue(serviceProvider, static serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetService<IForwarderHttpClientFactory>()
                    ?? new ForwarderHttpClientFactory(serviceProvider.GetRequiredService<ILogger<ForwarderHttpClientFactory>>());

                return httpClientFactory.CreateClient(new ForwarderHttpClientContext
                {
                    NewConfig = HttpClientConfig.Empty
                });
            });
        }
    }
}
