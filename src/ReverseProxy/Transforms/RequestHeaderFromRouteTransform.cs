using System;

namespace Yarp.ReverseProxy.Transforms;

public class RequestHeaderFromRouteTransform : RequestHeaderTransform
{
    public RequestHeaderFromRouteTransform(RequestHeaderTransformMode mode, string headerName, string routeValueKey)
        : base(mode, headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        if (string.IsNullOrEmpty(routeValueKey))
        {
            throw new ArgumentException($"'{nameof(routeValueKey)}' cannot be null or empty.", nameof(routeValueKey));
        }

        RouteValueKey = routeValueKey;
    }

    internal string RouteValueKey { get; }

    protected override string? GetValue(RequestTransformContext context)
    {
        var routeValues = context.HttpContext.Request.RouteValues;
        if (!routeValues.TryGetValue(RouteValueKey, out var value))
        {
            return null;
        }

        return value?.ToString();
    }
}

