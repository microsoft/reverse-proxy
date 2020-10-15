// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Composite destination health combining the passive and active health states.
    /// </summary>
    public struct CompositeDestinationHealth
    {
        public CompositeDestinationHealth(DestinationHealth passive, DestinationHealth active)
        {
            Passive = passive;
            Active = active;
            if (passive == DestinationHealth.Unknown && active == DestinationHealth.Unknown)
            {
                Current = DestinationHealth.Unknown;
            }
            else
            {
                Current = passive == DestinationHealth.Unhealthy || active == DestinationHealth.Unhealthy ? DestinationHealth.Unhealthy : DestinationHealth.Healthy;
            }
        }

        /// <summary>
        /// Passive health state.
        /// </summary>
        public DestinationHealth Passive { get; }

        /// <summary>
        /// Active health state.
        /// </summary>
        public DestinationHealth Active { get; }

        /// <summary>
        /// Current health state calculated based on <see cref="Passive"/> and <see cref="Active"/>.
        /// </summary>
        public DestinationHealth Current { get; }

        public CompositeDestinationHealth ChangePassive(DestinationHealth passive)
        {
            return new CompositeDestinationHealth(passive, Active);
        }

        public CompositeDestinationHealth ChangeActive(DestinationHealth active)
        {
            return new CompositeDestinationHealth(Passive, active);
        }
    }
}
