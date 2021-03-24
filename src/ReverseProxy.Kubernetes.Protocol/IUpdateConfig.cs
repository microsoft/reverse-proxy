using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.Kubernetes.Protocol
{
    public interface IUpdateConfig
    {
        void Update(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters);
    }
}
