// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <remarks>
    /// Three modes for endpoint (replica) selection.
    /// See implementation in SF Reverse Proxy: <see href="https://msazure.visualstudio.com/One/_git/WindowsFabric?path=%2Fsrc%2Fprod%2Fsrc%2FManagement%2FApplicationGateway%2FHttp%2FTargetReplicaSelector.h&amp;version=GBdevelop&amp;line=14&amp;lineEnd=19&amp;lineStartColumn=1&amp;lineEndColumn=1&amp;lineStyle=plain"/>.
    /// </remarks>
    internal static class StatefulReplicaSelectionLabel
    {
        // All endpoint has the chance to get traffic. Default option.
        internal const string All = "All";

        // Only primary endpoint has the chance to get traffic (stateful service).
        internal const string PrimaryOnly = "PrimaryOnly";

        // Only secondary has the chance to get traffic (stateful service).
        internal const string SecondaryOnly = "SecondaryOnly";
    }
}
