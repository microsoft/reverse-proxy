// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple request header values.
    /// </summary>
    public class RequestHeaderValueTransform : RequestTransform
    {
        public RequestHeaderValueTransform(string headerName, string value, bool append)
        {
            HeaderName = headerName ?? throw new ArgumentNullException(nameof(headerName));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Append = append;
        }

        internal string HeaderName { get; }

        internal string Value { get; }

        internal bool Append { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            StringValues values = default;
            if (Append)
            {
                values = TakeHeader(context, HeaderName);
            }
            else
            {
                RemoveHeader(context, HeaderName);
            }

            values = StringValues.Concat(values, Value);

            if (!StringValues.IsNullOrEmpty(values))
            {
                AddHeader(context, HeaderName, values);
            }

            return default;
        }
    }
}
