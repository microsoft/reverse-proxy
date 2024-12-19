// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

namespace Yarp.ReverseProxy.Sample;

public sealed class CustomConfigFilter : IProxyConfigFilter
{
    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        // How to use custom metadata to configure clusters
        if (cluster.Metadata?.TryGetValue("CustomHealth", out var customHealth) ?? false
            && string.Equals(customHealth, "true", StringComparison.OrdinalIgnoreCase))
        {
            cluster = cluster with
            {
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures,
                    },
                    Passive = cluster.HealthCheck?.Passive,
                }
            };
        }

        // Or wrap the meatadata in config sugar
        var config = new ConfigurationBuilder().AddInMemoryCollection(cluster.Metadata).Build();
        if (config.GetValue<bool>("CustomHealth"))
        {
            cluster = cluster with
            {
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures,
                    },
                    Passive = cluster.HealthCheck?.Passive,
                }
            };
        }

        return new ValueTask<ClusterConfig>(cluster);
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig cluster, CancellationToken cancel)
    {
        // Do not let config based routes take priority over code based routes.
        // Lower numbers are higher priority. Code routes default to 0.
        if (route.Order.HasValue && route.Order.Value < 1)
        {
            return new ValueTask<RouteConfig>(route with { Order = 1 });
        }

        return new ValueTask<RouteConfig>(route);
    }
}
