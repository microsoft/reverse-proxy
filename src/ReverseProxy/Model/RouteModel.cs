// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Immutable representation of the portions of a route
/// that only change in reaction to configuration changes.
/// </summary>
/// <remarks>
/// All members must remain immutable to avoid thread safety issues.
/// Instead, instances of <see cref="RouteModel"/> are replaced
/// in their entirety when values need to change.
/// </remarks>
public sealed class RouteModel
{
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public RouteModel(
        RouteConfig config,
        ClusterState? cluster,
        HttpTransformer transformer)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Cluster = cluster;
        Transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
    }

    /// <summary>
    ///  Creates a new instance contain weight clusters
    /// </summary>
    public RouteModel(
        RouteConfig config,
        ClusterState? cluster,
        HttpTransformer transformer,
        WeightedList<ClusterState> weightClusters)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Cluster = cluster;
        Transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        WeightClusters = weightClusters;
    }



    // May not be populated if the cluster config is missing. https://github.com/microsoft/reverse-proxy/issues/797
    /// <summary>
    /// The <see cref="ClusterState"/> instance associated with this route.
    /// </summary>
    public ClusterState? Cluster
    {
        get;
        private set;
    }

    /// <summary>
    /// Transforms to apply for this route.
    /// </summary>
    public HttpTransformer Transformer { get; }

    public WeightedList<ClusterState> WeightClusters { get; }

    /// <summary>
    /// The configuration data used to build this route.
    /// </summary>
    public RouteConfig Config { get; }

    internal bool HasConfigChanged(RouteConfig newConfig, ClusterState? cluster, int? routeRevision)
    {
        return Cluster != cluster || routeRevision != cluster?.Revision || !Config.Equals(newConfig);
    }

    internal void SetCluster(ClusterState cluster)
    {
        Cluster = cluster;
    }
}
