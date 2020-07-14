// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-For header with the previous clients's IP address.
    /// </summary>
    internal class RequestHeaderXForwardedForTransform : RequestHeaderTransform
    {
        public RequestHeaderXForwardedForTransform(bool append)
        {
            Append = append;
        }

        internal bool Append { get; }

        public override StringValues Apply(HttpContext context, StringValues values)
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
