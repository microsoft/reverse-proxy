// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.Service
{
    /// <summary>
    /// Represents a snapshot of proxy configuration data.
    /// </summary>
    public interface IProxyConfig
    {
        /// <summary>
        /// Route information for matching requests to clusters.
        /// </summary>
        IReadOnlyList<RouteConfig> Routes { get; }

        /// <summary>
        /// Cluster information for where to proxy requests to.
        /// </summary>
        IReadOnlyList<Cluster> Clusters { get; }

        /// <summary>
        /// A notification that triggers when this snapshot expires.
        /// </summary>
        IChangeToken ChangeToken { get; }
    }
}
