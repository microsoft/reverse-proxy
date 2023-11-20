using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

/// <summary>
/// Provides method to validate route configuration.
/// </summary>
public interface IRouteValidator
{
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors);
}
