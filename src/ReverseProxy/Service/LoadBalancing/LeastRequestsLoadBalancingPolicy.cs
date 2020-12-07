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
            var endpointCount = availableDestinations.Count;
            var leastRequestsEndpoint = availableDestinations[0];
            var leastRequestsCount = leastRequestsEndpoint.ConcurrencyCounter.Value;
            for (var i = 1; i < endpointCount; i++)
            {
                var endpoint = availableDestinations[i];
                var endpointRequestCount = endpoint.ConcurrencyCounter.Value;
                if (endpointRequestCount < leastRequestsCount)
                {
                    leastRequestsEndpoint = endpoint;
                    leastRequestsCount = endpointRequestCount;
                }
            }
            return leastRequestsEndpoint;
        }
    }
}
