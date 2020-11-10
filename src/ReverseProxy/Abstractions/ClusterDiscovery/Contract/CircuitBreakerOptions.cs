// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions
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

        internal CircuitBreakerOptions DeepClone()
        {
            return new CircuitBreakerOptions
            {
                MaxConcurrentRequests = MaxConcurrentRequests,
                MaxConcurrentRetries = MaxConcurrentRetries,
            };
        }

        internal static bool Equals(CircuitBreakerOptions options1, CircuitBreakerOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.MaxConcurrentRequests == options2.MaxConcurrentRequests
                && options1.MaxConcurrentRetries == options2.MaxConcurrentRetries;
        }
    }
}
