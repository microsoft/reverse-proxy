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
        public static string FailureRateLimitMetadataName => "TransportFailureRateHealthPolicy.RateLimit";

        /// <summary>
        /// Period of time while detected failures are kept and taken into account in the rate calculation.
        /// The default is 60 seconds.
        /// </summary>
        public TimeSpan DetectionWindowSize { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Default failure rate limit for a destination to be marked as unhealhty that is applied if it's not set on a cluster's metadata.
        /// It's calucated as a share of failed requests out of all requests proxied to the same destination in the <see cref="DetectionWindowSize"/> period.
        /// The value is in range (0,1). The default is 0.3.
        /// </summary>
        public double DefaultFailureRateLimit { get; set; } = 0.3;
    }
}
