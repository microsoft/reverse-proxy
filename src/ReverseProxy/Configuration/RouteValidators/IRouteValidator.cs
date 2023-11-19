using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

public interface IRouteValidator
{
    public ValueTask ValidateAsync(RouteMatch route, string routeId, IList<Exception> errors);
}
