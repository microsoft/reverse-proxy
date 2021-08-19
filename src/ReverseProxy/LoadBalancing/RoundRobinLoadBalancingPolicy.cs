// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.LoadBalancing
{
    internal sealed class RoundRobinLoadBalancingPolicy : ILoadBalancingPolicy
    {
        private readonly ConditionalWeakTable<ClusterState, AtomicCounter> _counters = new ();

        public string Name => LoadBalancingPolicies.RoundRobin;

        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            if (availableDestinations.Count == 0)
            {
                return null;
            }

            var counter = _counters.GetOrCreateValue(cluster);

            // Increment returns the new value and we want the first return value to be 0.
            var offset = counter.Increment() - 1;

            // Preventing negative indicies from being computed by masking off sign.
            // Ordering of index selection is consistent across all offsets.
            // There may be a discontinuity when the sign of offset changes.
            return availableDestinations[(offset & 0x7FFFFFFF) % availableDestinations.Count];
        }
    }
}
