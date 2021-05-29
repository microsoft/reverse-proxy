// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.Discovery;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    internal sealed class HealthyOrPanicDestinationsPolicy : HealthyAndUnknownDestinationsPolicy
    {
        public override string Name => HealthCheckConstants.AvailableDestinations.HealthyOrPanic;

        public override IReadOnlyList<DestinationState> GetAvailalableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations)
        {
            var availableDestinations = base.GetAvailalableDestinations(config, allDestinations);
            return availableDestinations.Count > 0 ? availableDestinations : allDestinations;
        }
    }
}
