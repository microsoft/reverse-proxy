using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class MethodsValidator : IRouteValidator
{
    private static readonly HashSet<string> _validMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "HEAD", "OPTIONS", "GET", "PUT", "POST", "PATCH", "DELETE", "TRACE",
    };

    public IList<Exception> Validate(RouteMatch route, string routeId)
    {
        // Methods are optional
        if (route.Methods is null)
        {
            return Array.Empty<Exception>();
        }

        var errors = new List<Exception>();
        var seenMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in route.Methods)
        {
            if (!seenMethods.Add(method))
            {
                errors.Add(new ArgumentException($"Duplicate HTTP method '{method}' for route '{routeId}'."));
                continue;
            }

            if (!_validMethods.Contains(method))
            {
                errors.Add(new ArgumentException($"Unsupported HTTP method '{method}' has been set for route '{routeId}'."));
            }
        }

        return errors;
    }
}
