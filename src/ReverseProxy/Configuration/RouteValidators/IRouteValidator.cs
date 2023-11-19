using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

public interface IRouteValidator
{
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors);
}
