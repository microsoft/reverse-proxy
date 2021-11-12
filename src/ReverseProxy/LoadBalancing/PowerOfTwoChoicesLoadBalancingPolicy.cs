// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.LoadBalancing;

internal sealed class PowerOfTwoChoicesLoadBalancingPolicy : ILoadBalancingPolicy
{
    private readonly IRandomFactory _randomFactory;

    public PowerOfTwoChoicesLoadBalancingPolicy(IRandomFactory randomFactory)
    {
        _randomFactory = randomFactory;
    }

    public string Name => LoadBalancingPolicies.PowerOfTwoChoices;

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations.Count == 0)
        {
            return null;
        }

        // Pick two, and then return the least busy. This avoids the effort of searching the whole list, but
        // still avoids overloading a single destination.
        var destinationCount = availableDestinations.Count;
        var random = _randomFactory.CreateRandomInstance();
        var first = availableDestinations[random.Next(destinationCount)];
        var second = availableDestinations[random.Next(destinationCount)];
        return (first.ConcurrentRequestCount <= second.ConcurrentRequestCount) ? first : second;
    }
}
