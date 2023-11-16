using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class HostValidator : IRouteValidator
{
    public void AddValidationErrors(RouteMatch route, string routeId, IList<Exception> errors)
    {
        if (route.Hosts is null || route.Hosts.Count == 0)
        {
            // Host is optional when Path is specified
            return;
        }

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
    }
}
