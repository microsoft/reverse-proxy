// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract
{
    /// <summary>
    /// Names of built-in load balancing policies.
    /// </summary>
    public static class LoadBalancingPolicies
    {
        /// <summary>
        /// Select the first endpoint without considering load. This is useful for dual endpoint fail-over systems.
        /// </summary>
        public static string First => nameof(First);

        /// <summary>
        /// Select an endpoint randomly.
        /// </summary>
        public static string Random => nameof(Random);

        /// <summary>
        /// Selects an endpoint by cycling through them in order.
        /// </summary>
        public static string RoundRobin => nameof(RoundRobin);

        /// <summary>
        /// Select the endpoint with the least assigned requests. This requires examining all nodes.
        /// </summary>
        public static string LeastRequests => nameof(LeastRequests);

        /// <summary>
        /// Select two random endpoints and then select the one with the least assigned requests.
        /// This avoids the overhead of LeastRequests and the worst case for Random where it selects a busy endpoint.
        /// </summary>
        public static string PowerOfTwoChoices => nameof(PowerOfTwoChoices);
    }
}
