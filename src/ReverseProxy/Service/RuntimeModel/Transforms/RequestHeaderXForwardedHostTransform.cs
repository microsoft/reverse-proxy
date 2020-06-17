// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Host header with the request's original Host header.
    /// </summary>
    internal class RequestHeaderXForwardedHostTransform : RequestHeaderTransform
    {
        // or Set
        private readonly bool _append;

        public RequestHeaderXForwardedHostTransform(bool append)
        {
            _append = append;
        }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var host = context.Request.Host;
            if (!host.HasValue)
            {
                return _append ? values : StringValues.Empty;
            }

            var encodedHost = host.ToUriComponent();
            return _append ? StringValues.Concat(values, encodedHost) : new StringValues(encodedHost);
        }
    }
}
