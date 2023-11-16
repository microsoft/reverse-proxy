using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class QueryParametersValidator : IRouteValidator
{
    public void AddValidationErrors(RouteMatch route, string routeId, IList<Exception> errors)
    {
        if (route.QueryParameters is null)
        {
            // Query Parameters are optional
            return;
        }

        foreach (var queryParameter in route.QueryParameters)
        {
            if (queryParameter is null)
            {
                errors.Add(new ArgumentException($"A null route query parameter has been set for route '{routeId}'."));
                continue;
            }

            if (string.IsNullOrEmpty(queryParameter.Name))
            {
                errors.Add(new ArgumentException($"A null or empty route query parameter name has been set for route '{routeId}'."));
            }

            if (queryParameter.Mode != QueryParameterMatchMode.Exists
                && (queryParameter.Values is null || queryParameter.Values.Count == 0))
            {
                errors.Add(new ArgumentException($"No query parameter values were set on route query parameter '{queryParameter.Name}' for route '{routeId}'."));
            }

            if (queryParameter.Mode == QueryParameterMatchMode.Exists && queryParameter.Values?.Count > 0)
            {
                errors.Add(new ArgumentException($"Query parameter values where set when using mode '{nameof(QueryParameterMatchMode.Exists)}' on route query parameter '{queryParameter.Name}' for route '{routeId}'."));
            }
        }
    }
}
