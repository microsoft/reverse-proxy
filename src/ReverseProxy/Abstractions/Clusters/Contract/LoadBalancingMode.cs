// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Load balancing strategies for endpoint selection.
    /// </summary>
    public enum LoadBalancingMode
    {
        /// <summary>
        /// Select two random endpoints and then select the one with the least assigned requests.
        /// This avoids the overhead of LeastRequests and the worst case for Random where it selects a busy endpoint.
        /// </summary>
        PowerOfTwoChoices,
        /// <summary>
        /// Select the endpoint with the least assigned requests. This requires examining all nodes.
        /// </summary>
        LeastRequests,
        /// <summary>
        /// Select an endpoint randomly.
        /// </summary>
        Random,
        /// <summary>
        /// Selects an endpoint by cycling through them in order.
        /// </summary>
        RoundRobin,
        /// <summary>
        /// Select the first endpoint without considering load. This is useful for dual endpoint fail-over systems.
        /// </summary>
        First,
    }
}
