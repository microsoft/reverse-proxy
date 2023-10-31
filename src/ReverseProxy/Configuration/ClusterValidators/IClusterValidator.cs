using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

internal interface IClusterValidator
{
    protected IList<Exception> Validate(ClusterConfig cluster);

    public bool IsValid(ClusterConfig cluster, out IList<Exception> errors)
    {
        errors = Validate(cluster);
        return errors.Count == 0;
    }
}
