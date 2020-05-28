// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract
{
    /// <summary>
    /// Names of built-in session affinity services.
    /// </summary>
    public static class SessionAffinityBuiltIns
    {
        public static class Modes
        {
            public static string Cookie => "Cookie";

            public static string CustomHeander => "CustomHeader";
        }

        public static class AffinityFailurePolicies
        {
            public static string Redistribute => "Redistribute";

            public static string Return503Error => "Return503Error";
        }
    }
}
