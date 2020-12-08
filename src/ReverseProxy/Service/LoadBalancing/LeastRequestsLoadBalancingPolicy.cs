// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.LoadBalancing
{
    internal sealed class LeastRequestsLoadBalancingPolicy : ILoadBalancingPolicy
    {
        public string Name => LoadBalancingConstants.Policies.LeastRequests;

        public DestinationInfo PickDestination(HttpContext context, IReadOnlyList<DestinationInfo> availableDestinations)
        {
            if (availableDestinations.Count == 0)
            {
                return null;
            }

            var destinationCount = availableDestinations.Count;
            var leastRequestsDestination = availableDestinations[0];
            var leastRequestsCount = leastRequestsDestination.ConcurrentRequestCount;
            for (var i = 1; i < destinationCount; i++)
            {
                var destination = availableDestinations[i];
                var endpointRequestCount = destination.ConcurrentRequestCount;
                if (endpointRequestCount < leastRequestsCount)
                {
                    leastRequestsDestination = destination;
                    leastRequestsCount = endpointRequestCount;
                }
            }
            return leastRequestsDestination;
        }
    }
}
