using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class FirstLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private static readonly ILoadBalancingStrategy _instance;
        public static readonly ILoadBalancingStrategy Instance = _instance ??= new FirstLoadBalancingStrategy();

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            return availableEndpoints[0];
        }
    }
}
