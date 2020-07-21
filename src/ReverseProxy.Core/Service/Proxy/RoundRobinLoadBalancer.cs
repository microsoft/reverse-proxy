using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace ReverseProxy.Core.Service.Proxy
{
    internal class RoundRobinLoadBalancer
    {
        private static Dictionary<int, EndpointCacheEntry> _cache = new Dictionary<int, EndpointCacheEntry>();
        const int MaxCacheSize = 100; 

        internal RoundRobinLoadBalancer()
        {

        }




        // A dirty way of getting a semi-unique hash for a list of endpoints
        // By adding the hashes of the endpoint configs together it avoids ordering and uses the value of the string for the hash



        private class EndpointCacheEntry
        {
            private readonly EndpointConfig[] _sortedConfigs;
            public DateTime LastAccessTime { get; private set; }
            private int _counter;
            private readonly int _hashcode;

            public EndpointConfig NextEndpoint()
            {
                LastAccessTime = DateTime.Now;
                return _sortedConfigs[Interlocked.Increment(ref _counter) % _sortedConfigs.Length];
            }

            public EndpointCacheEntry(IReadOnlyList<EndpointInfo> endpoints)
            {
                var configs = new List<EndpointConfig>();
                long hashcode = 0;
                foreach (var endpoint in endpoints)
                {
                    configs.Add(endpoint.Config.Value);
                    hashcode += endpoint.Config.Value.GetHashCode();
                }
                configs.Sort((c1, c2) =>
                {
                    return c1.GetHashCode().CompareTo(c2.GetHashCode());
                });

                _sortedConfigs = configs.ToArray();
                LastAccessTime = DateTime.Now;
                _counter = -1;
                _hashcode = (int)(hashcode & 0xFFFFFFFF);
            }

            public static int CalcEndpointsHashCode(IReadOnlyList<EndpointInfo> endpoints)
            {
                long result = 0;
                foreach (var ep in endpoints)
                {
                    result += ep.Config.Value.GetHashCode();
                }
                return (int)(result & 0xFFFFFFFF);
            }

            public override int GetHashCode()
            {
                return _hashcode;
            }

        }
    }


}
