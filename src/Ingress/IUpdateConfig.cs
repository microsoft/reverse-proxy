using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Configuration
{
    public interface IUpdateConfig
    {
        void Update(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters);
    }
}
