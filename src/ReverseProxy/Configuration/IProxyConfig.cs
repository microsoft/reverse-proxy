// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Represents a snapshot of proxy configuration data. These properties may be accessed multiple times and should not be modified.
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
