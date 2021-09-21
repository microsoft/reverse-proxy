// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.Telemetry.Consumption;

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

            // Interface that collects general metrics about the proxy forwarder
            services.AddSingleton<IForwarderMetricsConsumer, ForwarderMetricsConsumer>();

            // Registration of a consumer to events for proxy forwarder telemetry
            services.AddTelemetryConsumer<ForwarderTelemetryConsumer>();

            // Registration of a consumer to events for HttpClient telemetry
            // Note: this depends on changes implemented in .NET 5
            services.AddTelemetryConsumer<HttpClientTelemetryConsumer>();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            // Custom middleware that collects and reports the proxy metrics
            // Placed at the beginning so it is the first and last thing run for each request
            app.UsePerRequestMetricCollection();

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
            });
        }
    }
}
