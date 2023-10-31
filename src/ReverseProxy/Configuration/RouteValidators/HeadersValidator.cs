using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class HeadersValidator : IRouteValidator
{
    public IList<Exception> Validate(RouteMatch route, string routeId)
    {
        // Headers are optional
        if (route.Headers is null)
        {
            return Array.Empty<Exception>();
        }

        var errors = new List<Exception>();
        foreach (var header in route.Headers)
        {
            if (header is null)
            {
                errors.Add(new ArgumentException($"A null route header has been set for route '{routeId}'."));
                continue;
            }

            if (string.IsNullOrEmpty(header.Name))
            {
                errors.Add(new ArgumentException($"A null or empty route header name has been set for route '{routeId}'."));
            }

            if (header.Mode != HeaderMatchMode.Exists && header.Mode != HeaderMatchMode.NotExists
                                                      && (header.Values is null || header.Values.Count == 0))
            {
                errors.Add(new ArgumentException($"No header values were set on route header '{header.Name}' for route '{routeId}'."));
            }

            if ((header.Mode == HeaderMatchMode.Exists || header.Mode == HeaderMatchMode.NotExists) && header.Values?.Count > 0)
            {
                errors.Add(new ArgumentException($"Header values were set when using mode '{header.Mode}' on route header '{header.Name}' for route '{routeId}'."));
            }
        }

        return errors;
    }
}
