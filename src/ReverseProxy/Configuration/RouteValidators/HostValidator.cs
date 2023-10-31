using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class HostValidator : IRouteValidator
{
    public IList<Exception> Validate(RouteMatch route, string routeId)
    {
        // Host is optional when Path is specified
        if (route.Hosts is null || route.Hosts.Count == 0)
        {
            return Array.Empty<Exception>();
        }

        var errors = new List<Exception>();
        foreach (var host in route.Hosts)
        {
            if (string.IsNullOrEmpty(host))
            {
                errors.Add(new ArgumentException($"Empty host name has been set for route '{routeId}'."));
            }
            else if (host.Contains("xn--", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ArgumentException($"Punycode host name '{host}' has been set for route '{routeId}'. Use the unicode host name instead."));
            }
        }

        return errors;
    }
}
