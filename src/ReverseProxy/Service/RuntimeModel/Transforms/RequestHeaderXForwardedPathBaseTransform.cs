// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-PathBase header with the request's original PathBase.
    /// </summary>
    internal class RequestHeaderXForwardedPathBaseTransform : RequestHeaderTransform
    {

        public RequestHeaderXForwardedPathBaseTransform(bool append)
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

            var pathBase = context.Request.PathBase;
            if (!pathBase.HasValue)
            {
                return Append ? values : StringValues.Empty;
            }

            var encodedPathBase = pathBase.ToUriComponent();
            return Append ? StringValues.Concat(values, encodedPathBase) : new StringValues(encodedPathBase);
        }
    }
}
