using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;

namespace Yarp.Sample
{
    public interface IReproConfig
    {
        RouteConfig[] GetRoutes();
        ClusterConfig[] GetClusters();
    }

    public class ReproConfig : IReproConfig
    {
        
        private List<string> destservers;
        private List<string> destmachines;
        private readonly Hashtable unhealthycount = new Hashtable();

        public ReproConfig()
        {
           
        }


        public RouteConfig[] GetRoutes()
        {
            return new[]
            {
                new RouteConfig()
                {
                    RouteId = "route1",
                    ClusterId = "cluster1",
                    Match = new RouteMatch
                    {
                        // Path or Hosts are required for each route. This catch-all pattern matches all request paths.
                        Path = "{**catch-all}"
                    }
                }
            };
        }

        public ClusterConfig[] GetClusters()
        {
            
            IDictionary<string, DestinationConfig> destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);
            try
            {
                destservers = new List<string>() { "http://server1/", "http://server2" };
                destmachines = new List<string>() { "server1", "server" };
                unhealthycount.Clear();
                foreach (var srv in destservers)
                {
                    destinations.Add(srv.Replace("http://", ""), new DestinationConfig()
                    {
                        Address = srv
                    });
                    unhealthycount[srv] = "0";
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            IReadOnlyDictionary<string, DestinationConfig> dstns = destinations.ToImmutableDictionary();
            return new[]
            {
                new ClusterConfig()
                {
                    ClusterId = "cluster1",
                    LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
                    HealthCheck = new HealthCheckConfig
                    {
                        Active = new ActiveHealthCheckConfig
                        {
                            Enabled = true,
                            Interval = TimeSpan.FromSeconds(10),
                            Timeout = TimeSpan.FromSeconds(10),
                            Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures,
                            Path = "/check.htm",
                            OnUnhealthy = OnUnhealthy
                        }
                    },
                    Destinations = dstns
                }
            };
        }

        public void OnUnhealthy(string address)
        {
            var hc = Convert.ToInt32(unhealthycount[address]);
            hc++;
            unhealthycount[address] = Convert.ToString(hc);
            if (hc == 1)
            {
                Debug.Write("Server " + address + " unhealthy.");
            }
            else if (hc == 3)
            {
                Debug.Write("trying to restart " + address + ".");
                TryReset(address);
            }
            else if (hc == 5)
            {
                Debug.Write("Server " + address + " still unhealthy, please check!");
            }
        }

        private void TryReset(string address)
        {
            Debug.Write(address);
            //do smth to reset backend
        }

    }
}
