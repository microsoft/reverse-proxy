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
        /// The route model for the current request.
        /// </summary>
        RouteModel Route { get; }

        /// <summary>
        /// The cluster model for the current request.
        /// </summary>
        ClusterModel Cluster { get; }

        /// <summary>
        /// All destinations for the current cluster.
        /// </summary>
        IReadOnlyList<DestinationInfo> AllDestinations { get; }

        /// <summary>
        /// Cluster destinations that can handle the current request. This will initially include all destinations except those
        /// currently marked as unhealth if health checks are enabled.
        /// </summary>
        IReadOnlyList<DestinationInfo> AvailableDestinations { get; set; }

        /// <summary>
        /// The actual destination that the request was proxied to.
        /// </summary>
        DestinationInfo ProxiedDestination { get; set; }
    }
}
