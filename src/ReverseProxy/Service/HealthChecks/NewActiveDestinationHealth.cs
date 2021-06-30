// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Stores a new active health state for the given destination.
    /// </summary>
    public readonly struct NewActiveDestinationHealth
    {
        public NewActiveDestinationHealth(DestinationInfo destination, DestinationHealth newActiveHealth)
        {
            Destination = destination;
            NewActiveHealth = newActiveHealth;
        }

        public DestinationInfo Destination { get; }

        public DestinationHealth NewActiveHealth { get; }
    }
}
