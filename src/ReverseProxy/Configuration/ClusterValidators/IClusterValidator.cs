using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

public interface IClusterValidator
{
    public void AddValidationErrors(ClusterConfig cluster, IList<Exception> errors);
}
