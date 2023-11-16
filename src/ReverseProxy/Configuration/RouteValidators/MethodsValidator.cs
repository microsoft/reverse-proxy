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

    public void AddValidationErrors(RouteMatch route, string routeId, IList<Exception> errors)
    {
        if (route.Methods is null)
        {
            // Methods are optional
            return;
        }

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
    }
}
