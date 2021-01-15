// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.ServiceFabric
{
    internal enum StatefulReplicaSelectionMode
    {
        All,
        Primary,
        ActiveSecondary,
    }
}
