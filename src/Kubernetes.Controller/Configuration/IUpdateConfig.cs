using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Controller.Configuration;

public interface IUpdateConfig
{
    void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters);
}
