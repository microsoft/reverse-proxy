// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="ILoadBalancer"/>.
    /// </summary>
    internal class LoadBalancer : ILoadBalancer
    {
        private readonly IRandom _random;

        public LoadBalancer(IRandomFactory randomFactory)
        {
            _random = randomFactory.CreateRandomInstance();
        }

        public EndpointInfo PickEndpoint(
            IReadOnlyList<EndpointInfo> endpoints,
            in BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            var endpointCount = endpoints.Count;
            if (endpointCount == 0)
            {
                return null;
            }

            if (endpointCount == 1)
            {
                return endpoints[0];
            }

            switch (loadBalancingOptions.Mode)
            {
                case LoadBalancingMode.First:
                    return endpoints[0];
                case LoadBalancingMode.Random:
                    return endpoints[_random.Next(endpointCount)];
                case LoadBalancingMode.PowerOfTwoChoices:
                    // Pick two, and then return the least busy. This avoids the effort of searching the whole list, but
                    // still avoids overloading a single endpoint.
                    var firstEndpoint = endpoints[_random.Next(endpointCount)];
                    var secondEndpoint = endpoints[_random.Next(endpointCount)];
                    return (firstEndpoint.ConcurrencyCounter.Value <= secondEndpoint.ConcurrencyCounter.Value) ? firstEndpoint : secondEndpoint;
                case LoadBalancingMode.LeastRequests:
                    var leastRequestsEndpoint = endpoints[0];
                    var leastRequestsCount = leastRequestsEndpoint.ConcurrencyCounter.Value;
                    for (var i = 1; i < endpointCount; i++)
                    {
                        var endpoint = endpoints[i];
                        var endpointRequestCount = endpoint.ConcurrencyCounter.Value;
                        if (endpointRequestCount < leastRequestsCount)
                        {
                            leastRequestsEndpoint = endpoint;
                            leastRequestsCount = endpointRequestCount;
                        }
                    }
                    return leastRequestsEndpoint;
                default:
                    throw new NotSupportedException($"Load balancing mode '{loadBalancingOptions.Mode}' is not supported.");
            }
        }
    }
}
