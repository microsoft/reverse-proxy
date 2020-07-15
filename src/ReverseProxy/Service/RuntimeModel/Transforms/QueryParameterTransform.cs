// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal abstract class QueryParameterTransform : RequestParametersTransform
    {
        private readonly QueryStringTransformMode _mode;
        private readonly string _key;

        public QueryParameterTransform(QueryStringTransformMode mode, string key)
        {
            _mode = mode;
            _key = key;
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            string value = GetValue(context);
            if (!string.IsNullOrWhiteSpace(value))
            {
                switch (_mode)
                {
                    case QueryStringTransformMode.Append:
                        context.Query = context.Query.Add(_key, value.ToString());
                        break;
                    case QueryStringTransformMode.Set:
                        context.Query = SetQueryParameter(context.Query, _key, value);
                        break;
                    default:
                        throw new NotImplementedException(_mode.ToString());
                }
            }
        }

        protected abstract string GetValue(RequestParametersTransformContext context);

        private static QueryString SetQueryParameter(QueryString input, string key, object value)
        {
            var queryStringParameters = QueryHelpers.ParseQuery(input.Value);
            queryStringParameters[key] = value.ToString();

#if NETCOREAPP3_1
            var queryBuilder = new QueryBuilder(queryStringParameters.Select(pair => new System.Collections.Generic.KeyValuePair<string, string>(pair.Key, pair.Value.ToString())));
#elif NETCOREAPP5_0
            var queryBuilder = new QueryBuilder(queryStringParameters);
#else
#error A target framework was added to the project and needs to be added to this condition.
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
