// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.Sample
{
    /// <summary>
    /// ASP.NET Core pipeline initialization showing how to use IHttpProxy to directly handle proxying requests.
    /// With this approach you are responsible for destination discovery, load balancing, and related concerns.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpProxy();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app, IHttpProxy httpProxy)
        {
            // Configure our own HttpMessageInvoker for outbound calls for proxy operations
            var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            });

            // Setup our own request transform class
            var transformer = new CustomTransformer(); // or HttpTransformer.Default;
            var requestOptions = new RequestProxyOptions { Timeout = TimeSpan.FromSeconds(100) };

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                // When using IHttpProxy for direct proxying you are responsible for routing, destination discovery, load balancing, affinity, etc..
                // For an alternate example that includes those features see BasicYarpSample.
                endpoints.Map("/{**catch-all}", async httpContext =>
                {
                    await httpProxy.ProxyAsync(httpContext, "https://example.com", httpClient, requestOptions, transformer);
                    var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
                    
                    // Check if the proxy operation was successful
                    if (errorFeature != null)
                    {
                        var error = errorFeature.Error;
                        var exception = errorFeature.Exception;
                    }
                });
            });
        }

        /// <summary>
        /// Custom request transformation
        /// </summary>
        private class CustomTransformer : HttpTransformer
        {
            /// <summary>
            /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage
            /// fields are initialized except RequestUri, which will be initialized after the
            /// callback if no value is provided. The string parameter represents the destination
            /// URI prefix that should be used when constructing the RequestUri. The headers
            /// are copied by the base implementation, excluding some protocol headers like HTTP/2
            /// pseudo headers (":authority").
            /// </summary>
            /// <param name="httpContext">The incoming request.</param>
            /// <param name="proxyRequest">The outgoing proxy request.</param>
            /// <param name="destinationPrefix">The uri prefix for the selected destination server which can be used to create
            /// the RequestUri.</param>
            public override async Task TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
            {
                // Copy all request headers
                await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);

                // Customize the query string:
                var queryContext = new QueryTransformContext(httpContext.Request);
                queryContext.Collection.Remove("param1");
                queryContext.Collection["area"] = "xx2";

                // Assign the custom uri. Be careful about extra slashes when concatenating here.
                proxyRequest.RequestUri = new Uri(destinationPrefix + httpContext.Request.Path + queryContext.QueryString);

                // Suppress the original request header, use the one from the destination Uri.
                proxyRequest.Headers.Host = null;
            }
        }
    }
}
