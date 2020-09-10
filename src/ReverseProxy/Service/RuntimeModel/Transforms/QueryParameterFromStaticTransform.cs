// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class QueryParameterFromStaticTransform : QueryParameterTransform
    {
        private readonly string _value;

        public QueryParameterFromStaticTransform(QueryStringTransformMode mode, string key, string value)
            : base(mode, key)
        {
            _value = value;
        }

        protected override string GetValue(RequestParametersTransformContext context)
        {
            return _value;
        }
    }
}
