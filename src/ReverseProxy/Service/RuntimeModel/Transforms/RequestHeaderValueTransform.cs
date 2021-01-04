// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple request header values.
    /// </summary>
    public class RequestHeaderValueTransform : RequestHeaderTransform
    {
        public RequestHeaderValueTransform(string value, bool append)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Append = append;
        }

        internal string Value { get; }

        internal bool Append { get; }

        /// <inheritdoc/>
        public override StringValues Apply(HttpContext context, HttpRequestMessage proxyRequest, StringValues values)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
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
