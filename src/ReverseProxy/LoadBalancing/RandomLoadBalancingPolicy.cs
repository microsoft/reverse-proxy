// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;
using Yarp.ReverseProxy.Weighting;

namespace Yarp.ReverseProxy.LoadBalancing;

internal sealed class RandomLoadBalancingPolicy : ILoadBalancingPolicy
{
    private readonly IRandomFactory _randomFactory;
    private readonly IProxyWeightingProvider _proxyWeightingProvider;

    public RandomLoadBalancingPolicy(IRandomFactory randomFactory, IProxyWeightingProvider proxyWeightingProvider)
    {
        _randomFactory = randomFactory;
        _proxyWeightingProvider = proxyWeightingProvider;
    }

    public string Name => LoadBalancingPolicies.Random;

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations.Count == 0)
        {
            return null;
        }

        if (_proxyWeightingProvider is null)
        {
            var random = _randomFactory.CreateRandomInstance();
            return availableDestinations[random.Next(availableDestinations.Count)];
        }
        else
        {
            return WeightUtils.getRandomWeightedDestination(availableDestinations, _randomFactory);
        }
    }
}
