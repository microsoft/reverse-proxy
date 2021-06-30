// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Middleware
{
    /// <summary>
    /// Stores the current proxy configuration used when processing the request.
    /// </summary>
    public class ReverseProxyFeature : IReverseProxyFeature
    {
        /// <inheritdoc/>
        public RouteConfig RouteSnapshot { get; init; }

        /// <inheritdoc/>
        public ClusterConfig ClusterSnapshot { get; set; }

        /// <inheritdoc/>
        public IReadOnlyList<DestinationInfo> AllDestinations { get; init; }

        /// <inheritdoc/>
        public IReadOnlyList<DestinationInfo> AvailableDestinations { get; set; }

        /// <inheritdoc/>
        public DestinationInfo ProxiedDestination { get; set; }

    }
}
