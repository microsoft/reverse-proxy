// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class QueryStringRemoveTransform : RequestParametersTransform
    {
        private readonly string _key;

        public QueryStringRemoveTransform(string key)
        {
            _key = key;
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var queryStringParameters = QueryHelpers.ParseQuery(context.Query.Value);
            queryStringParameters.Remove(_key);

#if NETCOREAPP3_1
            var queryBuilder = new QueryBuilder(queryStringParameters.Select(pair => new System.Collections.Generic.KeyValuePair<string, string>(pair.Key, pair.Value.ToString())));
#elif NETCOREAPP5_0
            var queryBuilder = new QueryBuilder(queryStringParameters);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
            context.Query = queryBuilder.ToQueryString();
        }
    }
}
