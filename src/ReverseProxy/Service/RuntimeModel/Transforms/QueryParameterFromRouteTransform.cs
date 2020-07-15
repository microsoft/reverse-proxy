// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class QueryParameterRouteTransform : QueryParameterTransform
    {
        private readonly string _routeValueKey;

        public QueryParameterRouteTransform(QueryStringTransformMode mode, string key, string routeValueKey)
            : base(mode, key)
        {
            _routeValueKey = routeValueKey;
        }

        protected override string GetValue(RequestParametersTransformContext context)
        {
            var routeValues = context.HttpContext.Request.RouteValues;
            if (!routeValues.TryGetValue(_routeValueKey, out var value))
            {
                return null;
            }

            return value.ToString();
        }
    }
}
