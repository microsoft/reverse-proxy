// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Store current Cluster, ClusterConfig and Tracks proxy cluster destinations that are available to handle the current request.
    /// </summary>
    public interface IReverseProxyFeature
    {
        /// <summary>
        /// Cluster config for the the current request.
        /// </summary>
        ClusterConfig ClusterConfig { get; set; }

        /// <summary>
        /// Cluster destinations that can handle the current request.
        /// </summary>
        IReadOnlyList<DestinationInfo> AvailableDestinations { get; set; }
    }
}
