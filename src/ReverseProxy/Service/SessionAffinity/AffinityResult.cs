// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public readonly struct AffinityResult
    {
        public IReadOnlyList<DestinationInfo> Destinations { get; }

        public AffinityResult(IReadOnlyList<DestinationInfo> destinations)
        {
            Destinations = destinations;
        }
    }
}
