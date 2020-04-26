using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class CallbackLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private readonly Func<IEnumerable<EndpointInfo>, BackendConfig.BackendLoadBalancingOptions, EndpointInfo> _callback;

        public CallbackLoadBalancingStrategy(Func<IEnumerable<EndpointInfo>, BackendConfig.BackendLoadBalancingOptions, EndpointInfo> callback)
        {
            _callback = callback;
        }

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            return _callback(availableEndpoints, loadBalancingOptions);
        }
    }
}
