// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions
{
    public static class HealthCheckConstants
    {
        public static class PassivePolicy
        {
            public static string TransportFailureRate => "TransportFailureRate";
        }

        public static class ActivePolicy
        {
            public static string ConsequitiveFailures => "ConsequitiveFailures";
        }
    }
}