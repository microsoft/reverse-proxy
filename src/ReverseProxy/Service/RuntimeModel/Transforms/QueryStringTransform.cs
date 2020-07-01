 // Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class QueryStringTransform : RequestParametersTransform
    {
        private readonly QueryStringTransformMode _mode;
        private readonly string _key;
        private readonly string _value;

        public QueryStringTransform(QueryStringTransformMode mode, string key, string value)
        {
            _mode = mode;
            _key = key;
            _value = value;
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var parsedQueryString = QueryHelpers.ParseQuery(context.Query.Value);

            switch (_mode)
            {
                case QueryStringTransformMode.Append:
                    parsedQueryString.Add(_key, _value);
                    break;
                default:
                    throw new NotImplementedException(_mode.ToString());
            }

            context.Query = QueryString.Create(parsedQueryString);
        }
    }

    public enum QueryStringTransformMode
    {
        Append,
    }
}
