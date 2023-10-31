using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal interface IRouteValidator
{
    protected IList<Exception> Validate(RouteMatch route, string routeId);

    public bool IsValid(RouteMatch route, string routeId, out IList<Exception> errors)
    {
       errors = Validate(route, routeId);
       return errors.Count == 0;
    }
}
