// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Names of built-in session affinity services.
/// </summary>
public static class SessionAffinityConstants
{
    public static class Policies
    {
        public static string Cookie => nameof(Cookie);

        public static string CustomHeader => nameof(CustomHeader);
    }

    public static class FailurePolicies
    {
        public static string Redistribute => nameof(Redistribute);

        public static string Return503Error => nameof(Return503Error);
    }
}
