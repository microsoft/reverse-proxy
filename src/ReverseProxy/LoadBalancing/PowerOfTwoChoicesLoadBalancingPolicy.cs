// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;
using Yarp.ReverseProxy.Weighting;

namespace Yarp.ReverseProxy.LoadBalancing;

internal sealed class PowerOfTwoChoicesLoadBalancingPolicy : ILoadBalancingPolicy
{
    private readonly IRandomFactory _randomFactory;
    private readonly IProxyWeightingProvider _weightingProvider;

    public PowerOfTwoChoicesLoadBalancingPolicy(IRandomFactory randomFactory, IProxyWeightingProvider weightingProvider)
    {
        _randomFactory = randomFactory;
        _weightingProvider = weightingProvider;
    }

    public string Name => LoadBalancingPolicies.PowerOfTwoChoices;

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        var destinationCount = availableDestinations.Count;
        if (destinationCount == 0)
        {
            return null;
        }

        if (destinationCount == 1)
        {
            return availableDestinations[0];
        }

        // Pick two, and then return the least busy. This avoids the effort of searching the whole list, but
        // still avoids overloading a single destination.

        DestinationState first, second;
        if (_weightingProvider is null)
        {
            var random = _randomFactory.CreateRandomInstance();
            var firstIndex = random.Next(destinationCount);
            var secondIndex = random.Next(destinationCount - 1);
            // account for the firstIndex by skipping it and moving beyond its index in the list
            if (secondIndex >= firstIndex) { secondIndex++; }
            first = availableDestinations[firstIndex];
            second = availableDestinations[secondIndex];
        }
        else
        {
            first = WeightUtils.getRandomWeightedDestination(availableDestinations, _randomFactory);
            second = WeightUtils.getRandomWeightedDestinationWithSkip(availableDestinations, first, _randomFactory);
        }
        return (first.ConcurrentRequestCount <= second.ConcurrentRequestCount) ? first : second;
    }
}
