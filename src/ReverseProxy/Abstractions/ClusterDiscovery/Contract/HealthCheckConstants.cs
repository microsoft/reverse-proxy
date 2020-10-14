// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions
{
    public static class HealthCheckConstants
    {
        public static class PassivePolicy
        {
            public static readonly string TransportFailureRate = "TransportFailureRate";
        }

        public static class ActivePolicy
        {
            public static readonly string ConsecutiveFailures = "ConsecutiveFailures";
        }
    }
}
