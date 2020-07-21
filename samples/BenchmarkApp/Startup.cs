// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BenchmarkApp
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var urls = _configuration["urls"];
            var clusterUrls = _configuration["clusterUrls"];

            if (string.IsNullOrWhiteSpace(urls))
            {
                throw new ArgumentException("--urls is required");
            }

            if (string.IsNullOrWhiteSpace(clusterUrls))
            {
                throw new ArgumentException("--clusterUrls is required");
            }

            var configDictionary = new Dictionary<string, string>
            {
                { "Routes:0:RouteId", "route" },
                { "Routes:0:ClusterId", "cluster" },
                { "Routes:0:Match:Host", new Uri(urls.Split(';', 1)[0]).Host },
                { "Routes:0:Match:Path", "/{**catchall}" }
            };

            var clusterCount = 0;
            foreach (var clusterUrl in clusterUrls.Split(';'))
            {
                configDictionary.Add($"Clusters:cluster:Destinations:destination{clusterCount++}:Address", clusterUrl);
            }

            var proxyConfig = new ConfigurationBuilder().AddInMemoryCollection(configDictionary).Build();

            services.AddReverseProxy().LoadFromConfig(proxyConfig);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
            });
        }
    }
}
