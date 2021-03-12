// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Middleware;
using Microsoft.ReverseProxy.RuntimeModel;


namespace Yarp.Sample
{
    /// <summary>
    /// Initialiaztion for ASP.NET using YARP reverse proxy
    /// </summary>
    public class Startup
    {
        private const string DEBUG_HEADER = "Debug";
        private const string DEBUG_METADATA_KEY = "debug";
        private const string DEBUG_VALUE = "true";

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            // Specify a custom proxy config provider, in this case defined in InMemoryConfigProvider.cs
            // Programatically creating route and cluster configs. This allows loading the data from an arbitrary source.
            services.AddReverseProxy()
                .LoadFromMemory(GetRoutes(), GetClusters());
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                // We can customize the proxy pipeline and add/remove/replace steps
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    // Use a custom proxy middleware, defined below
                    proxyPipeline.Use(MyCustomProxyStep);
                    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
                    proxyPipeline.UseAffinitizedDestinationLookup();
                    proxyPipeline.UseProxyLoadBalancing();
                });
            });
        }

        private ProxyRoute[] GetRoutes()
        {
            return new[]
            {
                new ProxyRoute()
                {
                    RouteId = "route1",
                    ClusterId = "cluster1",
                    Match = new ProxyMatch
                    {
                        // Path or Hosts are required for each route. This catch-all pattern matches all request paths.
                        Path = "{**catch-all}"
                    }
                }
            };
        }
        private Cluster[] GetClusters()
        {
            var debugMetadata = new Dictionary<string, string>();
            debugMetadata.Add(DEBUG_METADATA_KEY, DEBUG_VALUE);

            return new[]
            {
                new Cluster()
                {
                    Id = "cluster1",
                    SessionAffinity = new SessionAffinityOptions { Enabled = true, Mode = "Cookie" },
                    Destinations = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1", new Destination() { Address = "https://example.com" } },
                        { "debugdestination1", new Destination() {
                            Address = "https://bing.com",
                            Metadata = debugMetadata  }
                        },
                    }
                }
            };
        }


        /// <summary>
        /// Custom proxy step that filters destinations based on a header in the inbound request
        /// Looks at each destination metadata, and filters in/out based on their debug flag and the inbound header
        /// </summary>
        public Task MyCustomProxyStep(HttpContext context, Func<Task> next)
        {
            // Can read data from the request via the context
            var useDebugDestinations = context.Request.Headers.TryGetValue(DEBUG_HEADER, out var headerValues) && headerValues.Count == 1 && headerValues[0] == DEBUG_VALUE;

            // The context also stores a ReverseProxyFeature which holds proxy specific data such as the cluster, route and destinations
            var availableDestinationsFeature = context.Features.Get<IReverseProxyFeature>();
            var filteredDestinations = new List<DestinationInfo>();

            // Filter destinations based on criteria
            foreach (var d in availableDestinationsFeature.AvailableDestinations)
            {
                //Todo: Replace with a lookup of metadata - but not currently exposed correctly here
                if (d.DestinationId.Contains("debug") == useDebugDestinations) { filteredDestinations.Add(d); }
            }
            availableDestinationsFeature.AvailableDestinations = filteredDestinations;

            // Important - required to move to the next step in the proxy pipeline
            return next();
        }
    }
}
