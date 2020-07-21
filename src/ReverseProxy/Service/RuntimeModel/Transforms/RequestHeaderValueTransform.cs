// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple request header values.
    /// </summary>
    internal class RequestHeaderValueTransform : RequestHeaderTransform
    {
        public RequestHeaderValueTransform(string value, bool append)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Append = append;
        }

        internal string Value { get; }

        internal bool Append { get; }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            if (Append)
            {
                return StringValues.Concat(values, Value);
            }

            // Set
            return Value;
        }
    }
}
