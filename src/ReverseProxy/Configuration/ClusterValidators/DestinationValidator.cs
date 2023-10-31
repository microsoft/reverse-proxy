using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

internal sealed class DestinationValidator : IClusterValidator
{
    public IList<Exception> Validate(ClusterConfig cluster)
    {
        if (cluster.Destinations is null)
        {
            return Array.Empty<Exception>();
        }

        var errors = new List<Exception>();
        foreach (var (name, destination) in cluster.Destinations)
        {
            if (string.IsNullOrEmpty(destination.Address))
            {
                errors.Add(new ArgumentException($"No address found for destination '{name}' on cluster '{cluster.ClusterId}'."));
            }
        }

        return errors;
    }
}
