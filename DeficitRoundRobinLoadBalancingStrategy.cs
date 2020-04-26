using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public class DeficitRoundRobinLoadBalancingStrategy : ILoadBalancingStrategy
    {
        private static readonly Dictionary<EndpointInfo, int> _counters = new Dictionary<EndpointInfo, int>();
        private readonly IDictionary<EndpointInfo, int> _quanta;

        public DeficitRoundRobinLoadBalancingStrategy(IDictionary<EndpointInfo, int> quanta)
        {
            _quanta = quanta;
        }

        public EndpointInfo Balance(IReadOnlyList<EndpointInfo> availableEndpoints, BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            foreach (var endpoint in availableEndpoints)
            {
                if (_quanta.TryGetValue(endpoint, out var quantum))
                {
                    if (!_counters.TryGetValue(endpoint, out var counter))
                    {
                        counter = 0;
                    }

                    if (counter < quantum)
                    {
                        _counters[endpoint] = counter + 1;

                        // reset all counters if we're at the end
                        if (endpoint == availableEndpoints.LastOrDefault() && counter == quantum - 1)
                        {
                            _counters.Clear();
                        }

                        return endpoint;
                    }
                }
            }

            return null;
        }
    }
}
