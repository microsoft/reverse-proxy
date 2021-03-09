// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Host header with the request's original Host header.
    /// </summary>
    public class RequestHeaderXForwardedHostTransform : RequestTransform
    {
        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="append">Indicates if the new value should append to or replace an existing header.</param>
        public RequestHeaderXForwardedHostTransform(string headerName, bool append)
        {
            HeaderName = headerName ?? throw new System.ArgumentNullException(nameof(headerName));
            Append = append;
        }

        internal string HeaderName { get; }
        internal bool Append { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var host = context.HttpContext.Request.Host;

            StringValues values = default;
            if (Append)
            {
                values = TakeHeader(context, HeaderName);
            }
            else
            {
                RemoveHeader(context, HeaderName);
            }

            if (host.HasValue)
            {
                values = StringValues.Concat(values, host.ToUriComponent());
            }

            if (!StringValues.IsNullOrEmpty(values))
            {
                AddHeader(context, HeaderName, values);
            }

            return default;
        }
    }
}
