// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Listener for changes in the clusters.
    /// </summary>
    public interface IClusterChangeListener
    {
        /// <summary>
        /// Gets called after a new <see cref="ClusterInfo"/> has been added.
        /// </summary>
        /// <param name="cluster">Added <see cref="ClusterInfo"/> instance.</param>
        void OnClusterAdded(ClusterInfo cluster);

        /// <summary>
        /// Gets called after an existing <see cref="ClusterInfo"/> has been changed.
        /// </summary>
        /// <param name="cluster">Changed <see cref="ClusterInfo"/> instance.</param>
        void OnClusterChanged(ClusterInfo cluster);

        /// <summary>
        /// Gets called after an existing <see cref="ClusterInfo"/> has been removed.
        /// </summary>
        /// <param name="cluster">Removed <see cref="ClusterInfo"/> instance.</param>
        void OnClusterRemoved(ClusterInfo cluster);
    }
}
