using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class PathValidator : IRouteValidator
{
    public ValueTask ValidateAsync(RouteMatch route, string routeId, IList<Exception> errors)
    {
        if (string.IsNullOrEmpty(route.Path))
        {
            // Path is optional when Host is specified
            return ValueTask.CompletedTask;
        }

        try
        {
            RoutePatternFactory.Parse(route.Path);
        }
        catch (RoutePatternException ex)
        {
            errors.Add(new ArgumentException($"Invalid path '{route.Path}' for route '{routeId}'.", ex));
        }

        return ValueTask.CompletedTask;
    }
}
