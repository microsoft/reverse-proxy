// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using Yarp.ReverseProxy.Telemetry.Consumption;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using Yarp.ReverseProxy.Middleware;

namespace Yarp.Sample
{
    /// <summary>
    /// ASP .NET Core pipeline initialization.
    /// </summary>
    public class Startup
    {
        private readonly IConfiguration _configuration;


        /// <summary>
        /// Initializes a new instance of the <see cref="Startup" /> class.
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
            services.AddReverseProxy()
                .LoadFromConfig(_configuration.GetSection("ReverseProxy"));

            services.AddHttpContextAccessor();

            //Enable metric collection for all the underlying event counters used by YARP
            services.AddAllPrometheusMetrics();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            //Configure the endpoint routing including the proxy
            app.UseEndpoints(endpoints =>
            {
                // We can customize the proxy pipeline and add/remove/replace steps
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    //Initialize our metrics collection
                    proxyPipeline.UsePerRequestMetricCollection();

                    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
                    proxyPipeline.UseSessionAffinity();
                    proxyPipeline.UseLoadBalancing();
                });

                // Add the /Metrics endpoint for prometheus to query on
                endpoints.MapMetrics();
            });
        }

    }
}
