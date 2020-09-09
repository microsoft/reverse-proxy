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
                throw new ArgumentNullException(nameof(context));
            }

            var value = GetValue(context);
            if (!string.IsNullOrEmpty(value))
            {
                switch (_mode)
                {
                    case QueryStringTransformMode.Append:
                        StringValues newValue = value;
                        if (context.Query.Collection.TryGetValue(_key, out var currentValue))
                        {
                             newValue = StringValues.Concat(currentValue, value);
                        }
                        context.Query.Collection[_key] = newValue;
                        break;
                    case QueryStringTransformMode.Set:
                        context.Query.Collection[_key] = value;
                        break;
                    default:
                        throw new NotImplementedException(_mode.ToString());
                }
            }
        }

        protected abstract string GetValue(RequestParametersTransformContext context);
    }

    internal enum QueryStringTransformMode
    {
        Append,
        Set
    }
}
