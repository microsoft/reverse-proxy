// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Circuit breaker options.
    /// </summary>
    public sealed class CircuitBreakerOptions
    {
        /// <summary>
        /// Maximum number of concurrent requests allowed before we start rejecting requests for a backend.
        /// </summary>
        public int MaxConcurrentRequests { get; set; }

        /// <summary>
        /// Maximum number of concurrent retries in flight allowed before we stop retrying requests for a backend.
        /// This helps reduce stress on a backend that may already be under extreme load.
        /// </summary>
        /// <remarks>
        /// This should always be less than or equal to <see cref="MaxConcurrentRequests"/>.
        /// </remarks>
        public int MaxConcurrentRetries { get; set; }

        internal CircuitBreakerOptions DeepClone()
        {
            return new CircuitBreakerOptions
            {
                MaxConcurrentRequests = MaxConcurrentRequests,
                MaxConcurrentRetries = MaxConcurrentRetries,
            };
        }
    }
}
