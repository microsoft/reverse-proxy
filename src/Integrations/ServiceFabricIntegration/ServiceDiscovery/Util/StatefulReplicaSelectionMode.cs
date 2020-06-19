// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    internal enum StatefulReplicaSelectionMode
    {
        All,
        Primary,
        ActiveSecondary,
    }
}
