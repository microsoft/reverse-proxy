// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryParameterFromStaticTransform : QueryParameterTransform
    {
        public QueryParameterFromStaticTransform(QueryStringTransformMode mode, string key, string value)
            : base(mode, key)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal string Value { get; }

        /// <inheritdoc/>
        protected override string GetValue(RequestTransformContext context)
        {
            return Value;
        }
    }
}
