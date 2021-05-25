// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    internal sealed class FallbackToAllDestinationsPolicy : StrictHealthyAndUnknownDestinationsPolicy
    {
        public override string Name => HealthCheckConstants.AvailableDestinations.FallbackToAll;

        public override IReadOnlyList<DestinationState> GetAvailalableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations)
        {
            var availableDestination = base.GetAvailalableDestinations(config, allDestinations);
            return availableDestination.Count > 0 ? availableDestination : allDestinations;
        }
    }
}
