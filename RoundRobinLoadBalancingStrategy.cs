using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class RoundRobinLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private static readonly ILoadBalancingStrategy _instance;
        public static readonly ILoadBalancingStrategy Instance = _instance ??= new RoundRobinLoadBalancingStrategy();

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            var offset = loadBalancingOptions.RoundRobinState.Increment();
            return availableEndpoints[offset % availableEndpoints.Count];
        }
    }
}
