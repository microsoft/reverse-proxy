// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.Middleware;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.LoadBalancing
{
    internal sealed class RoundRobinLoadBalancingPolicy : ILoadBalancingPolicy
    {
        private readonly ConditionalWeakTable<ClusterInfo, AtomicCounter> _counters = new ();

        public string Name => LoadBalancingConstants.Policies.RoundRobin;

        public DestinationInfo PickDestination(HttpContext context, IReadOnlyList<DestinationInfo> availableDestinations)
        {
            var counter = _counters.GetOrCreateValue(context.GetRequiredCluster());

            // Increment returns the new value and we want the first return value to be 0.
            var offset = counter.Increment() - 1;

            // Preventing negative indicies from being computed by masking off sign.
            // Ordering of index selection is consistent across all offsets.
            // There may be a discontinuity when the sign of offset changes.
            return availableDestinations[(offset & 0x7FFFFFFF) % availableDestinations.Count];
        }
    }
}
