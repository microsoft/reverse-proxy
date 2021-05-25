// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    internal abstract class BaseAvailableDestinationsPolicy : IAvaliableDestinationsPolicy
    {
        public abstract string Name { get; }

        public virtual IReadOnlyList<DestinationState> GetAvailalableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations)
        {
            var availableDestinations = allDestinations;
            var activeEnabled = (config.HealthCheck?.Active?.Enabled).GetValueOrDefault();
            var passiveEnabled = (config.HealthCheck?.Passive?.Enabled).GetValueOrDefault();

            if (activeEnabled || passiveEnabled)
            {
                availableDestinations = allDestinations.Where(destination =>
                {
                    // Only consider the current state if those checks are enabled.
                    var healthState = destination.Health;
                    var active = activeEnabled ? healthState.Active : DestinationHealth.Unknown;
                    var passive = passiveEnabled ? healthState.Passive : DestinationHealth.Unknown;

                    return IsDestinationAvailable(destination, active, passive);
                }).ToList();
            }

            return availableDestinations;
        }

        protected abstract bool IsDestinationAvailable(DestinationState destination, DestinationHealth activeHealth, DestinationHealth passiveHealth);
    }
}
