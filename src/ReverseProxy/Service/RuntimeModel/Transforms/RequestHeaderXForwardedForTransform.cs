// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-For header with the previous clients's IP address.
    /// </summary>
    public class RequestHeaderXForwardedForTransform : RequestTransform
    {
        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="headerName">The header name.</param>
        /// <param name="append">Indicates if the new value should append to or replace an existing header.</param>
        public RequestHeaderXForwardedForTransform(string headerName, bool append)
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

            var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();

            StringValues values = default;
            if (Append)
            {
                values = TakeHeader(context, HeaderName);
            }
            else
            {
                RemoveHeader(context, HeaderName);
            }

            if (remoteIp != null)
            {
                values = StringValues.Concat(values, remoteIp);
            }

            if (!StringValues.IsNullOrEmpty(values))
            {
                AddHeader(context, HeaderName, values);
            }

            return default;
        }
    }
}
