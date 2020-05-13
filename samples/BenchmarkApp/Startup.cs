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
            var backendUrls = _configuration["backendUrls"];

            if (string.IsNullOrWhiteSpace(urls))
            {
                throw new ArgumentException("--urls is required");
            }

            if (string.IsNullOrWhiteSpace(backendUrls))
            {
                throw new ArgumentException("--backendUrls is required");
            }

            var configDictionary = new Dictionary<string, string>
            {
                { "Routes:0:RouteId", "route" },
                { "Routes:0:BackendId", "backend" },
                { "Routes:0:Match:Host", new Uri(urls.Split(';', 1)[0]).Host },
            };

            var backendCount = 0;
            foreach (var backendUrl in backendUrls.Split(';'))
            {
                configDictionary.Add($"Backends:backend:Destinations:destination{backendCount++}:Address", backendUrl);
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
