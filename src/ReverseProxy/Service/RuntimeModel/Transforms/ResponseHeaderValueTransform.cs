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
    internal class ResponseHeaderValueTransform : ResponseHeaderTransform
    {
        private readonly string _value;
        private readonly bool _append;
        private readonly bool _always;

        public ResponseHeaderValueTransform(string value, bool append, bool always)
        {
            _value = value ?? throw new System.ArgumentNullException(nameof(value));
            _append = append;
            _always = always;
        }

        // Assumes the response status code has been set on the HttpContext already.
        public override StringValues Apply(HttpContext context, HttpResponseMessage response, StringValues values)
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
            if (_always || Success(context))
            {
                if (_append)
                {
                    result = StringValues.Concat(values, _value);
                }
                else
                {
                    result = _value;
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
