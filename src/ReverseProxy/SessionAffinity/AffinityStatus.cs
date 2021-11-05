// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Affinity resolution status.
/// </summary>
public enum AffinityStatus
{
    OK,
    AffinityKeyNotSet,
    AffinityKeyExtractionFailed,
    DestinationNotFound
}
