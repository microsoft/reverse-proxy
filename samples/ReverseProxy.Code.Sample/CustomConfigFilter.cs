// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.Sample
{
    public class CustomConfigFilter : IProxyConfigFilter
    {
        public Task ConfigureClusterAsync(Cluster cluster, CancellationToken cancel)
        {
            // How to use custom metadata to configure clusters
            if (cluster.Metadata != null
                && cluster.Metadata.TryGetValue("CustomHealth", out var customHealth)
                && string.Equals(customHealth, "true", StringComparison.OrdinalIgnoreCase))
            {
                cluster.HealthCheck ??= new HealthCheckOptions { Active = new ActiveHealthCheckOptions() };
                cluster.HealthCheck.Active.Enabled = true;
                cluster.HealthCheck.Active.Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures;
            }

            // Or wrap the metadata in config sugar
            var config = new ConfigurationBuilder().AddInMemoryCollection(cluster.Metadata).Build();
            if (config.GetValue<bool>("CustomHealth"))
            {
                cluster.HealthCheck ??= new HealthCheckOptions { Active = new ActiveHealthCheckOptions() };
                cluster.HealthCheck.Active.Enabled = true;
                cluster.HealthCheck.Active.Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures;
            }

            return Task.CompletedTask;
        }

        public Task<ProxyRoute> ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
        {
            // Do not let config based routes take priority over code based routes.
            // Lower numbers are higher priority. Code routes default to 0.
            if (route.Order.HasValue && route.Order.Value < 1)
            {
                return Task.FromResult(route with { Order = 1 });
            }

            return Task.FromResult(route);
        }
    }
}
