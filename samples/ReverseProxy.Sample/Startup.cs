// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Core.Abstractions;
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
            services.AddMemoryCache();
            services.AddReverseProxy()
                .LoadFromConfig(_configuration.GetSection("ReverseProxy"), reloadOnChange: true)
                .ConfigureBackendDefaults((id, backend) =>
                {
                    backend.HealthCheckOptions ??= new HealthCheckOptions();
                    // How to use custom metadata to configure backends
                    if (backend.Metadata?.TryGetValue("CustomHealth", out var customHealth) ?? false
                        && string.Equals(customHealth, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        backend.HealthCheckOptions.Enabled = true;
                    }
                })
                .ConfigureBackend("backend1", backend =>
                {
                    backend.HealthCheckOptions.Enabled = false;
                })
                .ConfigureRouteDefaults(route =>
                {
                    // Do not let config based routes take priority over code based routes.
                    // Lower numbers are higher priority.
                    if (route.Priority.HasValue && route.Priority.Value < 0)
                    {
                        route.Priority = 0;
                    }
                })
                // If I need services as part of the config:
                .ConfigureRoute<IMemoryCache>("route1", (route, cache) =>
                {
                    var value = cache.Get<int>("key");
                    route.Priority = value;
                })
                ;
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
                    // Custom endpoint selection
                    proxyPipeline.Use((context, next) =>
                    {
                        var someCriteria = false; // MeetsCriteria(context);
                        if (someCriteria)
                        {
                            var availableEndpointsFeature = context.Features.Get<IAvailableBackendEndpointsFeature>();
                            var endpoint = availableEndpointsFeature.Endpoints[0]; // PickEndpoint(availableEndpointsFeature.Endpoints);
                            // Load balancing will no-op if we've already reduced the list of available endpoints to 1.
                            availableEndpointsFeature.Endpoints = new[] { endpoint };
                        }

                        return next();
                    });
                    proxyPipeline.UseProxyLoadBalancing();
                });
            });
        }
    }
}
