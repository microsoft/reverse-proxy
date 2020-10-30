// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public sealed class ClusterDynamicState
    {
        public ClusterDynamicState(IReadOnlyList<DestinationInfo> allDestinations, bool healthChecksEnabled)
        {
            AllDestinations = allDestinations ?? throw new ArgumentNullException(nameof(allDestinations));
            HealthyDestinations = !healthChecksEnabled ? AllDestinations
                : allDestinations.Where(destination => destination.DynamicState?.Health.Current != DestinationHealth.Unhealthy).ToList().AsReadOnly();
        }

        public IReadOnlyList<DestinationInfo> AllDestinations { get; }

        public IReadOnlyList<DestinationInfo> HealthyDestinations { get; }
    }
}
