// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Middleware;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Telemetry.Consumption;

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
            var routes = new[]
            {
                new ProxyRoute()
                {
                    RouteId = "route1",
                    ClusterId = "cluster1",
                    Match = new ProxyMatch
                    {
                        Path = "{**catch-all}"
                    }
                }
            };
            var clusters = new[]
            {
                new Cluster()
                {
                    Id = "cluster1",
                    SessionAffinity = new SessionAffinityOptions { Enabled = true, Mode = "Cookie" },
                    Destinations = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1", new Destination() { Address = "https://localhost:10000" } }
                    }
                }
            };

            services.AddReverseProxy()
                .LoadFromMemory(routes, clusters)
                .AddTransformFactory<MyTransformFactory>()
                .AddTransforms<MyTransformFilter>()
                .AddTransforms(transformBuilderContext =>
                {
                    // For each route+cluster pair decide if we want to add transforms, and if so, which?
                    // This logic is re-run each time a route is rebuilt.

                    transformBuilderContext.AddPathPrefix("/prefix");

                    // Only do this for routes that require auth.
                    if (string.Equals("token", transformBuilderContext.Route.AuthorizationPolicy))
                    {
                        transformBuilderContext.AddRequestTransform(async transformContext =>
                        {
                            var user = transformContext.HttpContext.User;
                            var tokenService = transformContext.HttpContext.RequestServices.GetRequiredService<TokenService>();
                            var token = await tokenService.GetAuthTokenAsync(user);
                            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        });
                    }
                });

            services.AddHttpContextAccessor();
            services.AddSingleton<IProxyMetricsConsumer, ProxyMetricsConsumer>();
            services.AddScoped<IProxyTelemetryConsumer, ProxyTelemetryConsumer>();
            services.AddProxyTelemetryListener();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
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
                            var availableDestinationsFeature = context.Features.Get<IReverseProxyFeature>();
                            var destination = availableDestinationsFeature.AvailableDestinations[0]; // PickDestination(availableDestinationsFeature.Destinations);
                            // Load balancing will no-op if we've already reduced the list of available destinations to 1.
                            availableDestinationsFeature.AvailableDestinations = destination;
                        }

                        return next();
                    });
                    proxyPipeline.UseAffinitizedDestinationLookup();
                    proxyPipeline.UseProxyLoadBalancing();
                    proxyPipeline.UseRequestAffinitizer();
                });
            });
        }
    }
}
