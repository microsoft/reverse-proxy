// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Provides a method that applies a load balancing policy
    /// to select a destination.
    /// </summary>
    internal interface ILoadBalancer
    {
        /// <summary>
        /// Picks a destination to send traffic to.
        /// </summary>
        // TODO: How to ensure retries pick a different destination when available?
        DestinationInfo PickDestination(
            IReadOnlyList<DestinationInfo> availableDestinations,
            in ClusterConfig.ClusterLoadBalancingOptions loadBalancingOptions);
    }
}
