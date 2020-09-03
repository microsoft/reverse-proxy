// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Primitives;

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
            if (!string.IsNullOrEmpty(value))
            {
                switch (_mode)
                {
                    case QueryStringTransformMode.Append:
                        if (!context.Query.ModifiedQueryParameters.ContainsKey(_key))
                        {
                            context.Query.ModifiedQueryParameters.Add(_key, value.ToString());
                        }
                        else
                        {
                            var newValue = StringValues.Concat(context.Query.ModifiedQueryParameters[_key], value.ToString());
                            context.Query.ModifiedQueryParameters[_key] = newValue;
                        }
                        break;
                    case QueryStringTransformMode.Set:
                        context.Query.ModifiedQueryParameters[_key] = value;
                        break;
                    default:
                        throw new NotImplementedException(_mode.ToString());
                }
            }
        }

        protected abstract string GetValue(RequestParametersTransformContext context);
    }

    public enum QueryStringTransformMode
    {
        Append,
        Set
    }
}
