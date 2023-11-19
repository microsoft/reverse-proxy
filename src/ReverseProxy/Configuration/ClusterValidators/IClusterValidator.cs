using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

public interface IClusterValidator
{
    public ValueTask ValidateAsync(ClusterConfig cluster, IList<Exception> errors);
}
