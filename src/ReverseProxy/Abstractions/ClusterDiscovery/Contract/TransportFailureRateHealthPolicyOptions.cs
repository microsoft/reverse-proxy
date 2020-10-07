// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Defines options for the transport failure rate passive health policy.
    /// </summary>
    public class TransportFailureRateHealthPolicyOptions
    {
        /// <summary>
        /// Name of failure rate limit metadata parameter. Destination marked as unhealthy once this limit is reached.
        /// </summary>
        public const string FailureRateLimitMetadataName = "TransportFailureRateHealthPolicy.RateLimit";

        /// <summary>
        /// Period of time while detected failures are kept and taken into account in the rate calculation.
        /// In milliseconds.
        /// </summary>
        public long DetectionWindowSize { get; set; } = 60000;

        /// <summary>
        /// Default failure rate limit that is applied if it's not set on a cluster's metadata.
        /// </summary>
        public double DefaultFailureRateLimit { get; set; } = 0.5;
    }
}
