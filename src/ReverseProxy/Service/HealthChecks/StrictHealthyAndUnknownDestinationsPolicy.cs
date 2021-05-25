// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    // Policy marking destinations as available only if their active and passive health states
    /// are either 'Healthy' or 'Unknown'/>.
    internal class StrictHealthyAndUnknownDestinationsPolicy : BaseAvailableDestinationsPolicy
    {
        public override string Name => HealthCheckConstants.AvailableDestinations.StrictHealthyAndUnknown;

        protected override bool IsDestinationAvailable(DestinationState destination, DestinationHealth activeHealth, DestinationHealth passiveHealth)
        {
            // Filter out unhealthy ones. Unknown state is OK, all destinations start that way.
            return activeHealth != DestinationHealth.Unhealthy && passiveHealth != DestinationHealth.Unhealthy;
        }
    }
}
