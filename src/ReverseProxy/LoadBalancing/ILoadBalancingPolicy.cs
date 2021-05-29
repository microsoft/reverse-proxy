// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.LoadBalancing
{
    /// <summary>
    /// Provides a method that applies a load balancing policy
    /// to select a destination.
    /// </summary>
    public interface ILoadBalancingPolicy
    {
        /// <summary>
        ///  A unique identifier for this load balancing policy. This will be referenced from config.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Picks a destination to send traffic to.
        /// </summary>
        DestinationState? PickDestination(
            HttpContext context,
            IReadOnlyList<DestinationState> availableDestinations);
    }
}
