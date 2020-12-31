// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple response header or trailer values.
    /// </summary>
    public class ResponseHeaderValueTransform : ResponseTransform
    {
        public ResponseHeaderValueTransform(string name, string value, bool append, bool always)
        {
            Name = name ?? throw new System.ArgumentNullException(nameof(name));
            Value = value ?? throw new System.ArgumentNullException(nameof(value));
            Append = append;
            Always = always;
        }

        internal bool Always { get; }

        internal bool Append { get; }

        internal string Name { get; }

        internal string Value { get; }

        // Assumes the response status code has been set on the HttpContext already.
        /// <inheritdoc/>
        public override Task ApplyAsync(HttpContext context, HttpResponseMessage proxyResponse)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            if (proxyResponse is null)
            {
                throw new System.ArgumentNullException(nameof(proxyResponse));
            }

            if (Always || Success(context))
            {
                if (Append)
                {
                    context.Response.Headers.Append(Name, Value);
                }
                else if (string.IsNullOrEmpty(Value))
                {
                    context.Response.Headers.Remove(Name);
                }
                else
                {
                    context.Response.Headers[Name] = Value;
                }
            }

            return Task.CompletedTask;
        }

        private static bool Success(HttpContext context)
        {
            // TODO: How complex should this get? Compare with http://nginx.org/en/docs/http/ngx_http_headers_module.html#add_header
            return context.Response.StatusCode < 400;
        }
    }
}
