// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Abstractions.Config;
using Yarp.ReverseProxy.Middleware;
using Yarp.ReverseProxy.Telemetry.Consumption;

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
                .ConfigureHttpClient((context, handler) =>
                {
                    handler.Expect100ContinueTimeout = TimeSpan.FromMilliseconds(300);
                })
                .AddTransformFactory<MyTransformFactory>()
                .AddTransforms<MyTransformProvider>()
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
                            // AuthN and AuthZ will have already been completed after request routing.
                            var ticket = await transformContext.HttpContext.AuthenticateAsync("token");
                            var tokenService = transformContext.HttpContext.RequestServices.GetRequiredService<TokenService>();
                            var token = await tokenService.GetAuthTokenAsync(ticket.Principal);
                            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        });
                    }

                    transformBuilderContext.AddResponseTransform(context =>
                    {
                        // Suppress the response body from errors.
                        // The status code was already copied.
                        if (!context.ProxyResponse.IsSuccessStatusCode)
                        {
                            context.SuppressResponseBody = true;
                        }

                        return default;
                    });
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
                    proxyPipeline.UseSessionAffinity();
                    proxyPipeline.UseLoadBalancing();
                });
            });
        }
    }
}
