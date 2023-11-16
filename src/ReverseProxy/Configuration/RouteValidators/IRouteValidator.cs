using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

public interface IRouteValidator
{
    public void AddValidationErrors(RouteMatch route, string routeId, IList<Exception> errors);
}
