// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Proto header with the request's original url scheme.
    /// </summary>
    public class RequestHeaderXForwardedProtoTransform : RequestTransform
    {
        public RequestHeaderXForwardedProtoTransform(string headerName, bool append)
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

            StringValues values = default;
            if (Append)
            {
                values = TakeHeader(context, HeaderName);
            }
            else
            {
                RemoveHeader(context, HeaderName);
            }

            values = StringValues.Concat(values, context.HttpContext.Request.Scheme);

            AddHeader(context, HeaderName, values);

            return default;
        }
    }
}
