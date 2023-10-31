using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class PathValidator : IRouteValidator
{
    public IList<Exception> Validate(RouteMatch route, string routeId)
    {
        // Path is optional when Host is specified
        if (string.IsNullOrEmpty(route.Path))
        {
            return ImmutableList<Exception>.Empty;
        }

        var errors = new List<Exception>();
        try
        {
            RoutePatternFactory.Parse(route.Path);
        }
        catch (RoutePatternException ex)
        {
            errors.Add(new ArgumentException($"Invalid path '{route.Path}' for route '{routeId}'.", ex));
        }

        return errors;
    }
}
