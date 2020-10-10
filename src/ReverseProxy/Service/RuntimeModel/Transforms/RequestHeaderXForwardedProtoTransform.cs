// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Proto header with the request's original url scheme.
    /// </summary>
    public class RequestHeaderXForwardedProtoTransform : RequestHeaderTransform
    {

        public RequestHeaderXForwardedProtoTransform(bool append)
        {
            Append = append;
        }

        internal bool Append { get; }

        /// <inheritdoc/>
        public override StringValues Apply(HttpContext context, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var scheme = context.Request.Scheme;
            return Append ? StringValues.Concat(values, scheme) : new StringValues(scheme);
        }
    }
}
