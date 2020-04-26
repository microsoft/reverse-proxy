using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class RandomLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private readonly IRandomFactory _randomFactory;

        public RandomLoadBalancingStrategy(IRandomFactory randomFactory)
        {
            _randomFactory = randomFactory;
        }

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            var random = _randomFactory.CreateRandomInstance();
            return availableEndpoints[random.Next(availableEndpoints.Count)];
        }
    }
}
