using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class PowerOfTwoChoicesLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private readonly IRandomFactory _randomFactory;

        public PowerOfTwoChoicesLoadBalancingStrategy(IRandomFactory randomFactory)
        {
            _randomFactory = randomFactory;
        }

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            // Pick two, and then return the least busy. This avoids the effort of searching the whole list, but
            // still avoids overloading a single endpoint.
            var random1 = _randomFactory.CreateRandomInstance();
            var firstEndpoint = availableEndpoints[random1.Next(availableEndpoints.Count)];
            var secondEndpoint = availableEndpoints[random1.Next(availableEndpoints.Count)];
            return firstEndpoint.ConcurrencyCounter.Value <= secondEndpoint.ConcurrencyCounter.Value
                ? firstEndpoint
                : secondEndpoint;
        }
    }
}
