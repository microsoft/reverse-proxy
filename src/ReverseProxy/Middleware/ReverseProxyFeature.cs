// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Store current ClusterConfig and Tracks proxy cluster destinations that are available to handle the current request.
    /// </summary>
    public class ReverseProxyFeature : IReverseProxyFeature
    {
        /// <summary>
        /// Cluster config for the the current request.
        /// </summary>
        public ClusterConfig ClusterConfig { get; set; }

        /// <summary>
        /// Cluster destinations that can handle the current request.
        /// </summary>
        public IReadOnlyList<DestinationInfo> AvailableDestinations { get; set; }

        /// <summary>
        /// Actual destination chosen as the target that received the current request.
        /// </summary>
        public DestinationInfo SelectedDestination { get; set; }

    }
}
