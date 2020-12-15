// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Host header with the request's original Host header.
    /// </summary>
    public class RequestHeaderXForwardedHostTransform : RequestHeaderTransform
    {
        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="append">Indicates if the new value should append to or replace an existing header.</param>
        public RequestHeaderXForwardedHostTransform(bool append)
        {
            Append = append;
        }

        internal bool Append { get; }

        /// <inheritdoc/>
        public override StringValues Apply(HttpContext context, HttpRequestMessage request, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var host = context.Request.Host;
            if (!host.HasValue)
            {
                return Append ? values : StringValues.Empty;
            }

            var encodedHost = host.ToUriComponent();
            return Append ? StringValues.Concat(values, encodedHost) : new StringValues(encodedHost);
        }
    }
}
