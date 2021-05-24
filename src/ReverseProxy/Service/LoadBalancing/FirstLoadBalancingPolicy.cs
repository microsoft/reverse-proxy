// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.LoadBalancing
{
    internal sealed class FirstLoadBalancingPolicy : ILoadBalancingPolicy
    {
        public string Name => LoadBalancingPolicies.First;

        public DestinationState? PickDestination(HttpContext context, IReadOnlyList<DestinationState> availableDestinations)
        {
            if (availableDestinations.Count == 0)
            {
                return null;
            }

            var selectedDestination = availableDestinations[0];
            for (var i = 1; i < availableDestinations.Count; i++)
            {
                var destination = availableDestinations[i];
                if (string.Compare(selectedDestination.DestinationId, destination.DestinationId, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    selectedDestination = destination;
                }
            }

            return selectedDestination;
        }
    }
}
