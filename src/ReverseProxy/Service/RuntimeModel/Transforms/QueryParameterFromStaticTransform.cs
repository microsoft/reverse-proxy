// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryParameterFromStaticTransform : QueryParameterTransform
    {
        public QueryParameterFromStaticTransform(QueryStringTransformMode mode, string key, string value)
            : base(mode, key)
        {
            Value = value;
        }

        internal string Value { get; }

        /// <inheritdoc/>
        protected override string GetValue(RequestTransformContext context)
        {
            return Value;
        }
    }
}
