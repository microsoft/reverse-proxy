// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Sample;

/// <summary>
/// ASP .NET Core pipeline initialization.
/// </summary>
public class Startup
{
    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpForwarder();
    }

    /// <summary>
    /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    /// </summary>
    public void Configure(IApplicationBuilder app)
    {
        var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
            ConnectTimeout = TimeSpan.FromSeconds(15),
        });

        var transformBuilder = app.ApplicationServices.GetRequiredService<ITransformBuilder>();
        var transformer = transformBuilder.Create(context =>
        {
            context.AddQueryRemoveKey("param1");
            context.AddQueryValue("area", "xx2", false);
            context.AddOriginalHost(false);
        });

        // or var transformer = new CustomTransformer();
        // or var transformer = HttpTransformer.Default;

        var requestConfig = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapForwarder("/{**catch-all}", "https://example.com", requestConfig, transformer, httpClient);
        });
    }

    private class CustomTransformer : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
        {
            // Copy all request headers
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            // Customize the query string:
            var queryContext = new QueryTransformContext(httpContext.Request);
            queryContext.Collection.Remove("param1");
            queryContext.Collection["area"] = "xx2";

            // Assign the custom uri. Be careful about extra slashes when concatenating here.
            proxyRequest.RequestUri = new Uri(destinationPrefix + httpContext.Request.Path + queryContext.QueryString);

            // Suppress the original request header, use the one from the destination Uri.
            proxyRequest.Headers.Host = null;
        }

        public override ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
        {
            // Suppress the response body from errors.
            // The status code was already copied.
            if (!proxyResponse.IsSuccessStatusCode)
            {
                return new ValueTask<bool>(false);
            }

            return base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
        }
    }
}
