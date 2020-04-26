using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class LeastRequestsLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private static readonly ILoadBalancingStrategy _instance;
        public static readonly ILoadBalancingStrategy Instance = _instance ??= new LeastRequestsLoadBalancingStrategy();

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            var leastRequestsEndpoint = availableEndpoints[0];
            var leastRequestsCount = leastRequestsEndpoint.ConcurrencyCounter.Value;
            for (var i = 1; i < availableEndpoints.Count; i++)
            {
                var endpoint = availableEndpoints[i];
                var endpointRequestCount = endpoint.ConcurrencyCounter.Value;
                if (endpointRequestCount < leastRequestsCount)
                {
                    leastRequestsEndpoint = endpoint;
                    leastRequestsCount = endpointRequestCount;
                }
            }
            return leastRequestsEndpoint;
        }
    }
}
