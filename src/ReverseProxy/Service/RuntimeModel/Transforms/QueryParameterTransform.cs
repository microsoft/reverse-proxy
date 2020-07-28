// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

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
                        context.Query.AppendQueryParameter(_key, value.ToString());
                        break;
                    case QueryStringTransformMode.Set:
                        context.Query.SetQueryParameter(_key, value);
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
