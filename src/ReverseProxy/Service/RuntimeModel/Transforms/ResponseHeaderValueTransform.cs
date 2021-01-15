// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple response header values.
    /// </summary>
    public class ResponseHeaderValueTransform : ResponseTransform
    {
        public ResponseHeaderValueTransform(string headerName, string value, bool append, bool always)
        {
            HeaderName = headerName ?? throw new System.ArgumentNullException(nameof(headerName));
            Value = value ?? throw new System.ArgumentNullException(nameof(value));
            Append = append;
            Always = always;
        }

        internal bool Always { get; }

        internal bool Append { get; }

        internal string HeaderName { get; }

        internal string Value { get; }

        // Assumes the response status code has been set on the HttpContext already.
        /// <inheritdoc/>
        public override Task ApplyAsync(ResponseTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (Always || Success(context))
            {
                var existingHeader = TakeHeader(context, HeaderName);
                if (Append)
                {
                    var value = StringValues.Concat(existingHeader, Value);
                    SetHeader(context, HeaderName, value);
                }
                else if (!string.IsNullOrEmpty(Value))
                {
                    SetHeader(context, HeaderName, Value);
                }
                // If the given value is empty, any existing header is removed.
            }

            return Task.CompletedTask;
        }

        private static bool Success(ResponseTransformContext context)
        {
            // TODO: How complex should this get? Compare with http://nginx.org/en/docs/http/ngx_http_headers_module.html#add_header
            return context.HttpContext.Response.StatusCode < 400;
        }
    }
}
