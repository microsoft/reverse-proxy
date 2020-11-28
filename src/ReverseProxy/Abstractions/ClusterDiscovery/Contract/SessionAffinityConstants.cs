// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract
{
    /// <summary>
    /// Names of built-in session affinity services.
    /// </summary>
    public static class SessionAffinityConstants
    {
        public static class Modes
        {
            public static string Cookie => nameof(Cookie);

            public static string CustomHeader => nameof(CustomHeader);
        }

        public static class AffinityFailurePolicies
        {
            public static string Redistribute => nameof(Redistribute);

            public static string Return503Error => nameof(Return503Error);
        }
    }
}
