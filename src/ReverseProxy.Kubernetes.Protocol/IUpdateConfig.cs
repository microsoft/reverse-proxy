using System.Collections.Generic;
using Yarp.ReverseProxy.Discovery;

namespace Yarp.ReverseProxy.Kubernetes.Protocol
{
    public interface IUpdateConfig
    {
        void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters);
    }
}
