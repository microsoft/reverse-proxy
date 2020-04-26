using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class TrafficAllocationLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private readonly Func<IEnumerable<EndpointInfo>, IEnumerable<EndpointInfo>> _selector;
        private readonly int _variation;
        private readonly ILoadBalancingStrategy _backingLoadBalancingStrategy;

        public TrafficAllocationLoadBalancingStrategy(Func<IEnumerable<EndpointInfo>, IEnumerable<EndpointInfo>> selector, decimal? variation,
            ILoadBalancingStrategy backingLoadBalancingStrategy)
        {
            _selector = selector;
            _variation = (int)((variation ?? 0M) * 100);
            _backingLoadBalancingStrategy = backingLoadBalancingStrategy;
        }

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            var matching = _selector(availableEndpoints);
            return ThreadLocalRandom.Current.Next(0, 101) <= _variation
                ? _backingLoadBalancingStrategy.Balance(matching.ToList().AsReadOnly(), loadBalancingOptions)
                : _backingLoadBalancingStrategy.Balance(availableEndpoints.Except(matching).ToList().AsReadOnly(), loadBalancingOptions);
        }
    }
}
