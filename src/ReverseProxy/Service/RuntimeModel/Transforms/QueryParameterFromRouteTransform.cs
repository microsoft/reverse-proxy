// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryParameterRouteTransform : QueryParameterTransform
    {
        public QueryParameterRouteTransform(QueryStringTransformMode mode, string key, string routeValueKey)
            : base(mode, key)
        {
            RouteValueKey = routeValueKey;
        }

        internal string RouteValueKey { get; }

        /// <inheritdoc/>
        protected override string GetValue(RequestParametersTransformContext context)
        {
            var routeValues = context.HttpContext.Request.RouteValues;
            if (!routeValues.TryGetValue(RouteValueKey, out var value))
            {
                return null;
            }

            return value.ToString();
        }
    }
}
