// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.LoadBalancing
{
    internal sealed class LeastRequestsLoadBalancingPolicy : ILoadBalancingPolicy
    {
        public string Name => LoadBalancingPolicies.LeastRequests;

        public DestinationState? PickDestination(HttpContext context, IReadOnlyList<DestinationState> availableDestinations)
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
