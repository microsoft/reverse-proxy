// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Discovery
{
    /// <summary>
    /// Represents a snapshot of proxy configuration data.
    /// </summary>
    public interface IProxyConfig
    {
        /// <summary>
        /// Routes matching requests to clusters.
        /// </summary>
        IReadOnlyList<RouteConfig> Routes { get; }

        /// <summary>
        /// Cluster information for where to proxy requests to.
        /// </summary>
        IReadOnlyList<ClusterConfig> Clusters { get; }

        /// <summary>
        /// A notification that triggers when this snapshot expires.
        /// </summary>
        IChangeToken ChangeToken { get; }
    }
}
