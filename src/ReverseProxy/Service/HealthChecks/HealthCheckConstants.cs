// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    public static class HealthCheckConstants
    {
        public static class PassivePolicy
        {
            public static readonly string TransportFailureRate = nameof(TransportFailureRate);
        }

        public static class ActivePolicy
        {
            public static readonly string ConsecutiveFailures = nameof(ConsecutiveFailures);
        }

        public static class AvailableDestinations
        {
            /// <summary>
            /// Marks destination as available for proxying requests to if its health state
            /// is either 'Healthy' or 'Unknown'.
            /// </summary>
            /// <remarks>It applies only if active or passive health checks are enabled.</remarks>
            public static readonly string HealthyAndUnknown = nameof(HealthyAndUnknown);

            /// <summary>
            /// Calls <see cref="HealthyAndUnknown"/> policy at first to determine
            /// desninations' availability. If no available destinations are returned
            /// from this call, it marks all cluster's destination as available.
            /// </summary>
            public static readonly string HealthyOrPanic = nameof(HealthyOrPanic);
        }
    }
}
