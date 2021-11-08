// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace Yarp.Sample
{
    /// <summary>
    /// ASP.NET Core pipeline initialization showing how to use IHttpForwarder to directly handle forwarding requests.
    /// With this approach you are responsible for destination discovery, load balancing, and related concerns.
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
        public void Configure(IApplicationBuilder app, IHttpForwarder forwarder)
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
            var requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("/test/{**catch-all}", async httpContext =>
                {
                    var error = await forwarder.SendAsync(httpContext, "https://example.com", httpClient, requestOptions,
                        static (context, proxyRequest) =>
                        {
                            // Customize the query string:
                            var queryContext = new QueryTransformContext(context.Request);
                            queryContext.Collection.Remove("param1");
                            queryContext.Collection["area"] = "xx2";

                            // Assign the custom uri. Be careful about extra slashes when concatenating here.
                            proxyRequest.RequestUri = new Uri("https://example.com" + context.Request.Path + queryContext.QueryString);

                            // Suppress the original request header, use the one from the destination Uri.
                            proxyRequest.Headers.Host = null;

                            return default;
                        });

                    // Check if the proxy operation was successful
                    if (error != ForwarderError.None)
                    {
                        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
                        var exception = errorFeature.Exception;
                    }
                });


                // When using IHttpForwarder for direct forwarding you are responsible for routing, destination discovery, load balancing, affinity, etc..
                // For an alternate example that includes those features see BasicYarpSample.
                endpoints.Map("/{**catch-all}", async httpContext =>
                {
                    var error = await forwarder.SendAsync(httpContext, "https://example.com", httpClient, requestOptions, transformer);
                    // Check if the proxy operation was successful
                    if (error != ForwarderError.None)
                    {
                        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
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
            public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
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
