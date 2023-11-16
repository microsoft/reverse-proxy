using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class PathValidator : IRouteValidator
{
    public void AddValidationErrors(RouteMatch route, string routeId, IList<Exception> errors)
    {
        if (string.IsNullOrEmpty(route.Path))
        {
            // Path is optional when Host is specified
            return;
        }

        try
        {
            RoutePatternFactory.Parse(route.Path);
        }
        catch (RoutePatternException ex)
        {
            errors.Add(new ArgumentException($"Invalid path '{route.Path}' for route '{routeId}'.", ex));
        }
    }
}
