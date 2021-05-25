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
            public static readonly string StrictHealthyAndUnknown = nameof(StrictHealthyAndUnknown);

            public static readonly string FallbackToAll = nameof(FallbackToAll);
        }
    }
}
