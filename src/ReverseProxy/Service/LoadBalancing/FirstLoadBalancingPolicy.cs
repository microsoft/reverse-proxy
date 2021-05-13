// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.LoadBalancing
{
    internal sealed class FirstLoadBalancingPolicy : ILoadBalancingPolicy
    {
        public string Name => LoadBalancingPolicies.First;

        public DestinationState PickDestination(HttpContext context, IReadOnlyList<DestinationState> availableDestinations)
        {
            if (availableDestinations.Count == 0)
            {
                return null;
            }

            return availableDestinations[0];
        }
    }
}
