// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
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
}
