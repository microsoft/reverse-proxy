// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Host header with the request's original Host header.
    /// </summary>
    public class RequestHeaderXForwardedHostTransform : RequestHeaderTransform
    {
        public RequestHeaderXForwardedHostTransform(bool append)
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
