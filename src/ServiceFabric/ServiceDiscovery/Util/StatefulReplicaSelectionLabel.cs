// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.ServiceFabric;

/// <remarks>
/// Three modes for endpoint (replica) selection.
/// See implementation in SF Reverse Proxy: <see href="https://github.com/microsoft/service-fabric/blob/1e118f02294c99b61e676c07ac97283ee12197d4/src/prod/src/Management/ApplicationGateway/Http/TargetReplicaSelector.h#L14-L18"/>.
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
