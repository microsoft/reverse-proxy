// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.ServiceFabric;

internal enum StatefulReplicaSelectionMode
{
    All,
    Primary,
    ActiveSecondary,
}
