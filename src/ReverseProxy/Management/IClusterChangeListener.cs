// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Management
{
    /// <summary>
    /// Listener for changes in the clusters.
    /// </summary>
    public interface IClusterChangeListener
    {
        /// <summary>
        /// Gets called after a new <see cref="ClusterState"/> has been added.
        /// </summary>
        /// <param name="cluster">Added <see cref="ClusterState"/> instance.</param>
        void OnClusterAdded(ClusterState cluster);

        /// <summary>
        /// Gets called after an existing <see cref="ClusterState"/> has been changed.
        /// </summary>
        /// <param name="cluster">Changed <see cref="ClusterState"/> instance.</param>
        void OnClusterChanged(ClusterState cluster);

        /// <summary>
        /// Gets called after an existing <see cref="ClusterState"/> has been removed.
        /// </summary>
        /// <param name="cluster">Removed <see cref="ClusterState"/> instance.</param>
        void OnClusterRemoved(ClusterState cluster);
    }
}
