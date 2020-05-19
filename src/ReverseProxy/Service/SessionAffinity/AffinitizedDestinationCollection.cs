// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public readonly struct AffinitizedDestinationCollection
    {
        public readonly IReadOnlyList<DestinationInfo> Destinations;

        public readonly object RequestKey;

        public AffinitizedDestinationCollection(IReadOnlyList<DestinationInfo> destinations, object requestKey)
        {
            Destinations = destinations;
            RequestKey = requestKey;
        }
    }
}
