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

        private readonly YarpPrometheusMetrics metrics;


        /// <summary>
        /// Initializes a new instance of the <see cref="Startup" /> class.
        /// </summary>
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
            metrics = new YarpPrometheusMetrics();
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
            services.AddSingleton<IProxyMetricsConsumer, ProxyMetricsConsumer>();


            // Register our telemetry consumers for each of the types of proxy telemetry
            services.AddScoped<IProxyTelemetryConsumer, ProxyTelemetryConsumer>();
            services.AddProxyTelemetryListener();

#if NET5_0
            services.AddHttpTelemetryListener();
#endif
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseHttpMetrics();
            app.UseEndpoints(endpoints =>
            {
                // We can customize the proxy pipeline and add/remove/replace steps
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                
                    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
                    proxyPipeline.UseAffinitizedDestinationLookup();
                    proxyPipeline.UseProxyLoadBalancing();
                    // Use a custom proxy middleware, defined below
                    proxyPipeline.Use(metrics.ReportForYarp);
                });
                endpoints.MapMetrics();
            });
        }

    }
}
