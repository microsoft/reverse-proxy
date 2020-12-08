// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.LoadBalancing
{
    internal sealed class FirstLoadBalancingPolicy : ILoadBalancingPolicy
    {
        public string Name => LoadBalancingPolicies.First;

        public DestinationInfo PickDestination(HttpContext context, IReadOnlyList<DestinationInfo> availableDestinations)
        {
            if (availableDestinations.Count == 0)
            {
                return null;
            }

            return availableDestinations[0];
        }
    }
}
