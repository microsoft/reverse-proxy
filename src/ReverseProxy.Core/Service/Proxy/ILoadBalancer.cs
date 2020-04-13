// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Service.Proxy
{
    /// <summary>
    /// Provides a method that applies a load balancing policy
    /// to select a backend endpoint.
    /// </summary>
    public interface ILoadBalancer
    {
        /// <summary>
        /// Picks an endpoint to send traffic to.
        /// </summary>
        // TODO: How to ensure retries pick a different endpoint when available?
        EndpointInfo PickEndpoint(
            IReadOnlyList<EndpointInfo> availableEndpoints,
            in BackendConfig.BackendLoadBalancingOptions loadBalancingOptions);
    }
}
