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
            cluster.HealthCheckOptions ??= new HealthCheckOptions();
            // How to use custom metadata to configure clusters
            if (cluster.Metadata?.TryGetValue("CustomHealth", out var customHealth) ?? false
                && string.Equals(customHealth, "true", StringComparison.OrdinalIgnoreCase))
            {
                cluster.HealthCheckOptions.Enabled = true;
            }

            // Or wrap the meatadata in config sugar
            var config = new ConfigurationBuilder().AddInMemoryCollection(cluster.Metadata).Build();
            cluster.HealthCheckOptions.Enabled = config.GetValue<bool>("CustomHealth");

            return Task.CompletedTask;
        }

        public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
        {
            // Do not let config based routes take priority over code based routes.
            // Lower numbers are higher priority.
            if (route.Priority.HasValue && route.Priority.Value < 0)
            {
                route.Priority = 0;
            }

            return Task.CompletedTask;
        }
    }
}
