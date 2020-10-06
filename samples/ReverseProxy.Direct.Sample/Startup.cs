// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.Net.Http.Headers;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Sample
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
            services.AddControllers();
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
            var proxyOptions = new RequestProxyOptions()
            {
                RequestTimeout = TimeSpan.FromSeconds(100),
                // Copy all request headers except Host
                Transforms = new Transforms(
                    copyRequestHeaders: true,
                    requestTransforms: Array.Empty<RequestParametersTransform>(),
                    requestHeaderTransforms: new Dictionary<string, RequestHeaderTransform>()
                    {
                        { HeaderNames.Host, new RequestHeaderValueTransform(string.Empty, append: false) }
                    },
                    responseHeaderTransforms: new Dictionary<string, ResponseHeaderTransform>(),
                    responseTrailerTransforms: new Dictionary<string, ResponseHeaderTransform>())
            };

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.Map("/{**catch-all}", async httpContext =>
                {
                    await httpProxy.ProxyAsync(httpContext, "https://localhost:10000/", httpClient, proxyOptions);
                });
            });
        }
    }
}
