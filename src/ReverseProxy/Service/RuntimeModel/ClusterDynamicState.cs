// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    internal sealed class ClusterDynamicState
    {
        public ClusterDynamicState(
            IReadOnlyList<DestinationInfo> allDestinations,
            IReadOnlyList<DestinationInfo> healthyDestinations)
        {
            AllDestinations = allDestinations ?? throw new ArgumentNullException(nameof(allDestinations));
            HealthyDestinations = healthyDestinations ?? throw new ArgumentNullException(nameof(healthyDestinations));
        }

        public IReadOnlyList<DestinationInfo> AllDestinations { get; }

        public IReadOnlyList<DestinationInfo> HealthyDestinations { get; }
    }
}
