// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-For header with the previous clients's IP address.
    /// </summary>
    public class RequestHeaderXForwardedForTransform : RequestHeaderTransform
    {
        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="append">Indicates if the new value should append to or replace an existing header.</param>
        public RequestHeaderXForwardedForTransform(bool append)
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

            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (remoteIp == null)
            {
                return Append ? values : StringValues.Empty;
            }

            return Append ? StringValues.Concat(values, remoteIp) : new StringValues(remoteIp);
        }
    }
}
