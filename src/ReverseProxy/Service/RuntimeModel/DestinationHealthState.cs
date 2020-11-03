// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Composite destination health combining the passive and active health states.
    /// </summary>
    public class DestinationHealthState
    {
        private volatile DestinationHealth _active;
        private volatile DestinationHealth _passive;

        /// <summary>
        /// Passive health state.
        /// </summary>
        public DestinationHealth Passive
        {
            get => _passive;
            set => _passive = value;
        }

        /// <summary>
        /// Active health state.
        /// </summary>
        public DestinationHealth Active
        {
            get => _active;
            set => _active = value;
        }
    }
}
