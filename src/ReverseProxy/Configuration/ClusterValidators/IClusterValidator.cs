using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

/// <summary>
/// Provides method to validate cluster configuration.
/// </summary>
public interface IClusterValidator
{
    public ValueTask ValidateAsync(ClusterConfig cluster, IList<Exception> errors);
}
