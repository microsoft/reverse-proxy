// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="ILoadBalancer"/>.
    /// </summary>
    internal class LoadBalancer : ILoadBalancer
    {
        public EndpointInfo PickEndpoint(
            IReadOnlyList<EndpointInfo> healthyEndpoints,
            IReadOnlyList<EndpointInfo> allEndpoints,
            in BackendConfig.BackendLoadBalancingOptions loadBalancingOptions)
        {
            switch (loadBalancingOptions.Mode)
            {
                case BackendConfig.BackendLoadBalancingOptions.LoadBalancingMode.First:
                    // TODO: Remove, this is a silly load balancing mode
                    if (healthyEndpoints.Count == 0)
                    {
                        return null;
                    }

                    return healthyEndpoints[0];
                case BackendConfig.BackendLoadBalancingOptions.LoadBalancingMode.Random:
                    throw new NotImplementedException();
                case BackendConfig.BackendLoadBalancingOptions.LoadBalancingMode.PowerOfTwoChoices:
                    throw new NotImplementedException();
                default:
                    throw new GatewayException($"Load balancing mode '{loadBalancingOptions.Mode}' is not supported.");
            }
        }
    }
}
