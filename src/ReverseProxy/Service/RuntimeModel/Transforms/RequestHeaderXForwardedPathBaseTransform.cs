// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-PathBase header with the request's original PathBase.
    /// </summary>
    public class RequestHeaderXForwardedPathBaseTransform : RequestTransform
    {
        public RequestHeaderXForwardedPathBaseTransform(string headerName, bool append)
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

            var pathBase = context.HttpContext.Request.PathBase;

            StringValues values = default;
            if (Append)
            {
                values = TakeHeader(context, HeaderName);
            }
            else
            {
                RemoveHeader(context, HeaderName);
            }

            if (pathBase.HasValue)
            {
                values = StringValues.Concat(values, pathBase.ToUriComponent());
            }

            if (!StringValues.IsNullOrEmpty(values))
            {
                AddHeader(context, HeaderName, values);
            }

            return default;
        }
    }
}
