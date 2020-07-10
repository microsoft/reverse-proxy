// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class QueryStringParameterTransform : RequestParametersTransform
    {
        private readonly QueryStringTransformMode _mode;
        private readonly string _key;
        private readonly string _routeValueKey;

        public QueryStringParameterTransform(QueryStringTransformMode mode, string key, string routeValueKey)
        {
            _mode = mode;
            _key = key;
            _routeValueKey = routeValueKey;
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var routeValues = context.HttpContext.Request.RouteValues;
            if (routeValues.TryGetValue(_routeValueKey, out var value))
            {
                switch (_mode)
                {
                    case QueryStringTransformMode.Append:
                        context.Query = context.Query.Add(_key, value.ToString());
                        break;
                    case QueryStringTransformMode.Set:
                        context.Query = SetQueryStringParameter(context.Query, _key, value);
                        break;
                    default:
                        throw new NotImplementedException(_mode.ToString());
                }
            }
        }

        private static QueryString SetQueryStringParameter(QueryString input, string key, object value)
        {
            var queryStringParameters = QueryHelpers.ParseQuery(input.Value);
            queryStringParameters[key] = value.ToString();

#if NETCOREAPP3_1
            var queryBuilder = new QueryBuilder(queryStringParameters.Select(pair => new System.Collections.Generic.KeyValuePair<string, string>(pair.Key, pair.Value.ToString())));
#else
            var queryBuilder = new QueryBuilder(queryStringParameters);
#endif
            return queryBuilder.ToQueryString();
        }
    }

    public enum QueryStringTransformMode
    {
        Append,
        Set
    }
}
