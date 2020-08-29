// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Circuit breaker options.
    /// </summary>
    public sealed class CircuitBreakerOptions
    {
        /// <summary>
        /// Maximum number of concurrent requests allowed before we start rejecting requests for a cluster.
        /// </summary>
        public int MaxConcurrentRequests { get; set; }

        /// <summary>
        /// Maximum number of concurrent retries in flight allowed before we stop retrying requests for a cluster.
        /// This helps reduce stress on a cluster that may already be under extreme load.
        /// </summary>
        /// <remarks>
        /// This should always be less than or equal to <see cref="MaxConcurrentRequests"/>.
        /// </remarks>
        public int MaxConcurrentRetries { get; set; }
    }
}
