// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.LoadBalancing
{
    /// <summary>
    /// Select the alphabetically first available destination without considering load. This is useful for dual destination fail-over systems.
    /// </summary>
    internal sealed class FirstLoadBalancingPolicy : ILoadBalancingPolicy
    {
        public string Name => LoadBalancingPolicies.FirstAlphabetical;

        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
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
