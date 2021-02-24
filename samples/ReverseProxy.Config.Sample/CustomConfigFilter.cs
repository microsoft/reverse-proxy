// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.Sample
{
    public class CustomConfigFilter : IProxyConfigFilter
    {
        public ValueTask<Cluster> ConfigureClusterAsync(Cluster cluster, CancellationToken cancel)
        {
            return new ValueTask<Cluster>(cluster);
        }

        public ValueTask<ProxyRoute> ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
        {
            // Example: do not let config based routes take priority over code based routes.
            // Lower numbers are higher priority. Code routes default to 0.
            if (route.Order.HasValue && route.Order.Value < 1)
            {
                return new ValueTask<ProxyRoute>(route with { Order = 1 });
            }

            return new ValueTask<ProxyRoute>(route);
        }
    }
}
