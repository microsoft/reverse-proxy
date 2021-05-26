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
            var availableDestinations = base.GetAvailalableDestinations(config, allDestinations);
            return availableDestinations.Count > 0 ? availableDestinations : allDestinations;
        }
    }
}
