// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Abstractions
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
        /// <summary>
        /// Let a callback decide with endpoint to pick.
        /// </summary>
        Callback,
        /// <summary>
        /// Selects an endpoint by cycling through them in order, taking a quantum into account.
        /// </summary>
        DeficitRoundRobin,
        /// <summary>
        /// Selects the same endpoint until it is unavailable.
        /// </summary>
        FailOver,
        /// <summary>
        /// Selects an endpoint based on traffic allocation.
        /// </summary>
        TrafficAllocation
    }
}
