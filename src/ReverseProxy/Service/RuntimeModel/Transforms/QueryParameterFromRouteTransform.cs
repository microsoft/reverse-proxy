// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryParameterRouteTransform : QueryParameterTransform
    {
        public QueryParameterRouteTransform(QueryStringTransformMode mode, string key, string routeValueKey)
            : base(mode, key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
            }

            if (string.IsNullOrEmpty(routeValueKey))
            {
                throw new ArgumentException($"'{nameof(routeValueKey)}' cannot be null or empty.", nameof(routeValueKey));
            }

            RouteValueKey = routeValueKey;
        }

        internal string RouteValueKey { get; }

        /// <inheritdoc/>
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
}
