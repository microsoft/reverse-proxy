// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Middleware
{
    /// <summary>
    /// Stores the current proxy configuration used when processing the request.
    /// </summary>
    public interface IReverseProxyFeature
    {
        /// <summary>
        /// Route config for the current request.
        /// </summary>
        RouteConfig RouteSnapshot { get; }

        /// <summary>
        /// Cluster config for the current request.
        /// </summary>
        ClusterConfig ClusterSnapshot { get; }

        /// <summary>
        /// All destinations for the current cluster.
        /// </summary>
        IReadOnlyList<DestinationInfo> AllDestinations { get; }

        /// <summary>
        /// Cluster destinations that can handle the current request.
        /// </summary>
        IReadOnlyList<DestinationInfo> AvailableDestinations { get; set; }

        /// <summary>
        /// The actual destination that the request was proxied to.
        /// </summary>
        DestinationInfo ProxiedDestination { get; set; }
    }
}
