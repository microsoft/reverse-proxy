using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class MethodsValidator : IRouteValidator
{
    private static readonly HashSet<string> _validMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "HEAD", "OPTIONS", "GET", "PUT", "POST", "PATCH", "DELETE", "TRACE",
    };

    public ValueTask ValidateAsync(RouteMatch route, string routeId, IList<Exception> errors)
    {
        if (route.Methods is null)
        {
            // Methods are optional
            return ValueTask.CompletedTask;
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

        return ValueTask.CompletedTask;
    }
}
