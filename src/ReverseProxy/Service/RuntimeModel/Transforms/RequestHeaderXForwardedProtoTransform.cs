// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Proto header with the request's original url scheme.
    /// </summary>
    internal class RequestHeaderXForwardedProtoTransform : RequestHeaderTransform
    {
        // or Set
        private readonly bool _append;

        public RequestHeaderXForwardedProtoTransform(bool append)
        {
            _append = append;
        }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var scheme = context.Request.Scheme;
            return _append ? StringValues.Concat(values, scheme) : new StringValues(scheme);
        }
    }
}
