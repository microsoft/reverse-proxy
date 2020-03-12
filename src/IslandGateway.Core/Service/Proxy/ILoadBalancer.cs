// <copyright file="ILoadBalancer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service.Proxy
{
    /// <summary>
    /// Provides a method that applies a load balancing policy
    /// to select a backend endpoint.
    /// </summary>
    internal interface ILoadBalancer
    {
        /// <summary>
        /// Picks an endpoint to send traffic to.
        /// </summary>
        // TODO: How to ensure retries pick a different endpoint when available?
        EndpointInfo PickEndpoint(
            IReadOnlyList<EndpointInfo> healthyEndpoints,
            IReadOnlyList<EndpointInfo> allEndpoints,
            in BackendConfig.BackendLoadBalancingOptions loadBalancingOptions);
    }
}