// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
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
        var httpClientFactory = endpoints.ServiceProvider.GetService<IForwarderHttpClientFactory>()
            ?? new ForwarderHttpClientFactory(endpoints.ServiceProvider.GetRequiredService<ILogger<ForwarderHttpClientFactory>>());

        var httpClient = httpClientFactory.CreateClient(new ForwarderHttpClientContext
        {
            NewConfig = HttpClientConfig.Empty
        });

        return endpoints.MapForwarder(pattern, destinationPrefix, requestConfig, transformer, httpClient);
    }

    /// <summary>
    /// Adds direct forwarding of HTTP requests that match the specified pattern to a specific destination using customized configuration for the outgoing request, customized transforms, and customized HTTP client.
    /// </summary>
    public static IEndpointConventionBuilder MapForwarder(this IEndpointRouteBuilder endpoints, string pattern, string destinationPrefix, ForwarderRequestConfig requestConfig, HttpTransformer transformer, HttpMessageInvoker httpClient)
    {
        var forwarder = endpoints.ServiceProvider.GetRequiredService<IHttpForwarder>();

        return endpoints.Map(pattern, async httpContext =>
        {
            await forwarder.SendAsync(httpContext, destinationPrefix, httpClient, requestConfig, transformer);
        });
    }
}
