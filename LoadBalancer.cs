// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;
using ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies;

namespace Microsoft.ReverseProxy.Core.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="ILoadBalancer"/>.
    /// </summary>
    internal class LoadBalancer : ILoadBalancer
    {
        private readonly IRandomFactory _randomFactory;

        public LoadBalancer(IRandomFactory randomFactory)
        {
            _randomFactory = randomFactory;
        }

        private ILoadBalancingStrategy GetLoadBalancingStrategy(LoadBalancingMode loadBalancingMode, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            return loadBalancingMode switch
            {
                LoadBalancingMode.PowerOfTwoChoices => new PowerOfTwoChoicesLoadBalancingStrategy(_randomFactory),
                LoadBalancingMode.LeastRequests => LeastRequestsLoadBalancingStrategy.Instance,
                LoadBalancingMode.Random => new RandomLoadBalancingStrategy(_randomFactory),
                LoadBalancingMode.RoundRobin => RoundRobinLoadBalancingStrategy.Instance,
                LoadBalancingMode.First => FirstLoadBalancingStrategy.Instance,
                LoadBalancingMode.Callback => new CallbackLoadBalancingStrategy(loadBalancingOptions.Callback),
                LoadBalancingMode.DeficitRoundRobin => new DeficitRoundRobinLoadBalancingStrategy(loadBalancingOptions.DeficitRoundRobinQuanta),
                LoadBalancingMode.FailOver => new FailOverLoadBalancingStrategy(loadBalancingOptions.FailOverPreferredEndpoint(),
                    loadBalancingOptions.FailOverIsAvailablePredicate, loadBalancingOptions.FailOverFallBackLoadBalancingStrategy),
                LoadBalancingMode.TrafficAllocation => new TrafficAllocationLoadBalancingStrategy(loadBalancingOptions.TrafficAllocationSelector,
                    loadBalancingOptions.TrafficAllocationVariation, loadBalancingOptions.TrafficAllocationBackingLoadBalancingStrategy()),
                _ => throw new NotSupportedException($"Load balancing mode '{loadBalancingMode}' is not supported.")
            };
        }

        public EndpointInfo PickEndpoint(
            IReadOnlyList<EndpointInfo> availableEndpoints,
            in BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            if (availableEndpoints.Count == 0)
            {
                return null;
            }

            if (availableEndpoints.Count == 1)
            {
                return availableEndpoints[0];
            }

            var loadBalancingStrategy = GetLoadBalancingStrategy(loadBalancingOptions.Mode, loadBalancingOptions);
            return loadBalancingStrategy.Balance(availableEndpoints, loadBalancingOptions);
        }
    }
}
