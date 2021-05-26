// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.RuntimeModel
{
    public sealed class ClusterDestinationsState
    {
        public ClusterDestinationsState(
            IReadOnlyList<DestinationState> allDestinations,
            IReadOnlyList<DestinationState> healthyDestinations)
        {
            AllDestinations = allDestinations ?? throw new ArgumentNullException(nameof(allDestinations));
            HealthyDestinations = healthyDestinations ?? throw new ArgumentNullException(nameof(healthyDestinations));
        }

        public IReadOnlyList<DestinationState> AllDestinations { get; }

        public IReadOnlyList<DestinationState> HealthyDestinations { get; }
    }
}
