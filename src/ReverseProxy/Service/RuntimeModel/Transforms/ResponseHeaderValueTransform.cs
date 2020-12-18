// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple response header or trailer values.
    /// </summary>
    public class ResponseHeaderValueTransform : ResponseHeaderTransform
    {
        public ResponseHeaderValueTransform(string value, bool append, bool always)
        {
            Value = value ?? throw new System.ArgumentNullException(nameof(value));
            Append = append;
            Always = always;
        }

        internal bool Always { get; }

        internal bool Append { get; }

        internal string Value { get; }

        // Assumes the response status code has been set on the HttpContext already.
        /// <inheritdoc/>
        public override StringValues Apply(HttpContext context, HttpResponseMessage proxyResponse, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            if (response is null)
            {
                throw new System.ArgumentNullException(nameof(response));
            }

            var result = values;
            if (Always || Success(context))
            {
                if (Append)
                {
                    result = StringValues.Concat(values, Value);
                }
                else
                {
                    result = Value;
                }
            }

            return result;
        }

        private bool Success(HttpContext context)
        {
            // TODO: How complex should this get? Compare with http://nginx.org/en/docs/http/ngx_http_headers_module.html#add_header
            return context.Response.StatusCode < 400;
        }
    }
}
