// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Abstractions.Config;
using Yarp.ReverseProxy.Service;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Sample
{
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
            services.AddHttpProxy();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app, IHttpProxy httpProxy)
        {
            var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
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

            var requestOptions = new RequestProxyOptions { Timeout = TimeSpan.FromSeconds(100) };

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("/{**catch-all}", async httpContext =>
                {
                    await httpProxy.ProxyAsync(httpContext, "https://example.com", httpClient, requestOptions, transformer);
                    var errorFeature = httpContext.GetProxyErrorFeature();
                    if (errorFeature != null)
                    {
                        var error = errorFeature.Error;
                        var exception = errorFeature.Exception;
                    }
                });
            });
        }

        private class CustomTransformer : HttpTransformer
        {
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

            public override ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
            {
                // Suppress the response body from errors.
                // The status code was already copied.
                if (!proxyResponse.IsSuccessStatusCode)
                {
                    return new ValueTask<bool>(false);
                }

                return base.TransformResponseAsync(httpContext, proxyResponse);
            }
        }
    }
}
