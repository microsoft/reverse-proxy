using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Yarp.ReverseProxy.Configuration;

public class ConfigSubstitutionFilter : IProxyConfigFilter
{
    // Matches {{env_var_name}} or {{my-name}} or {{123name}} etc.
    private readonly Regex _exp = new("\\{\\{(\\w+\\-?\\w+?)\\}\\}");
    private readonly IConfiguration _configuration;

    public ConfigSubstitutionFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        if (cluster.Destinations is null)
        {
            return new ValueTask<ClusterConfig>(cluster);
        }

        // Each cluster has a dictionary of destinations, which is read-only, so we'll create a new one with our updates 
        var newDests = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in cluster.Destinations)
        {
            var origAddress = d.Value.Address;
            if (_exp.IsMatch(origAddress))
            {
                var lookup = _exp.Matches(origAddress)[0].Groups[1].Value;
                var newAddress = _configuration.GetValue<string>(lookup);

                if (string.IsNullOrWhiteSpace(newAddress))
                {
                    throw new System.ArgumentException($"Configuration Filter Error: Substitution for '{lookup}' in cluster '{d.Key}' not found in configuration.");
                }

                var modifiedDest = d.Value with { Address = newAddress };
                newDests.Add(d.Key, modifiedDest);
            }
            else
            {
                newDests.Add(d.Key, d.Value);
            }
        }

        return new ValueTask<ClusterConfig>(cluster with { Destinations = newDests });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
        => new ValueTask<RouteConfig>(route);
}
