// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy;

public interface IProxyStateLookup
{
    bool TryGetRoute(string id, [NotNullWhen(true)] out RouteModel? route);

    IEnumerable<RouteModel> GetRoutes();

    bool TryGetCluster(string id, [NotNullWhen(true)] out ClusterState? cluster);

    IEnumerable<ClusterState> GetClusters();
}
