// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public readonly struct AffinitizedDestinationCollection
    {
        public readonly IReadOnlyList<DestinationInfo> Destinations;

        public readonly string RequestKey;

        public AffinitizedDestinationCollection(IReadOnlyList<DestinationInfo> destinations, string requestKey)
        {
            Destinations = destinations;
            RequestKey = requestKey;
        }

        public bool RequestKeyFound => RequestKey != null;
    }
}
