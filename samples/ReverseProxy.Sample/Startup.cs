// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Core.Configuration.DependencyInjection;
using Microsoft.ReverseProxy.Core.Middleware;

namespace Microsoft.ReverseProxy.Sample
{
    /// <summary>
    /// ASP .NET Core pipeline initialization.
    /// </summary>
    public class Startup
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddReverseProxy().LoadFromConfig(_configuration.GetSection("ReverseProxy"), reloadOnChange: true);
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    proxyPipeline.UseProxyLoadBalancing();
                    // Customize the request before forwarding
                    proxyPipeline.Use((context, next) =>
                    {
                        var connection = context.Connection;
                        context.Request.Headers.AppendCommaSeparatedValues("X-Forwarded-For",
                            new IPEndPoint(connection.RemoteIpAddress, connection.RemotePort).ToString());
                        return next();
                    });
                });
            });
        }
    }
}
