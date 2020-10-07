// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.RuntimeModel
{
    /// <summary>
    /// Listener for changes in the destinations.
    /// </summary>
    public interface IDestinationChangeListener
    {
        /// <summary>
        /// Gets called after a new <see cref="DestinationInfo"/> has been added.
        /// </summary>
        /// <param name="destination">Added <see cref="DestinationInfo"/> instance.</param>
        void OnDestinationAdded(DestinationInfo destination);

        /// <summary>
        /// Gets called after an existing <see cref="DestinationInfo"/> has been changed.
        /// </summary>
        /// <param name="destination">Changed <see cref="DestinationInfo"/> instance.</param>
        void OnDestinationChanged(DestinationInfo destination);

        /// <summary>
        /// Gets called after an existing <see cref="DestinationInfo"/> has been removed.
        /// </summary>
        /// <param name="destination">Removed <see cref="DestinationInfo"/> instance.</param>
        void OnDestinationRemoved(DestinationInfo destination);
    }
}
