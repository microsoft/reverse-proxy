// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.LoadBalancing
{
    internal sealed class RandomLoadBalancingPolicy : ILoadBalancingPolicy
    {
        private readonly IRandomFactory _randomFactory;

        public RandomLoadBalancingPolicy(IRandomFactory randomFactory)
        {
            _randomFactory = randomFactory;
        }

        public string Name => LoadBalancingPolicies.Random;

        public DestinationInfo PickDestination(HttpContext context, IReadOnlyList<DestinationInfo> availableDestinations)
        {
            if (availableDestinations.Count == 0)
            {
                return null;
            }

            var random = _randomFactory.CreateRandomInstance();
            return availableDestinations[random.Next(availableDestinations.Count)];
        }
    }
}
