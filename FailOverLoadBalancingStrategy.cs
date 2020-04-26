using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class FailOverLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private readonly EndpointInfo _preferredEndpoint;
        private readonly Predicate<EndpointInfo> _isAvailable;
        private readonly ILoadBalancingStrategy _fallBackLoadBalancingStrategy;

        public FailOverLoadBalancingStrategy(EndpointInfo preferredEndpoint, Predicate<EndpointInfo> isAvailable, Func<ILoadBalancingStrategy> fallBackLoadBalancingStrategyFactory = null)
        {
            _preferredEndpoint = preferredEndpoint;
            _isAvailable = isAvailable;

            _fallBackLoadBalancingStrategy = fallBackLoadBalancingStrategyFactory();
        }

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            var exceptPreferred = availableEndpoints
                .Except(Enumerable.Repeat(_preferredEndpoint, 1))
                .ToList();

            return _isAvailable(_preferredEndpoint)
                ? _preferredEndpoint
                : _fallBackLoadBalancingStrategy.Balance(exceptPreferred.ToList().AsReadOnly(), loadBalancingOptions);
        }
    }
}
