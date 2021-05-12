// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Removes a response header.
    /// </summary>
    public class ResponseHeaderRemoveTransform : ResponseTransform
    {
        public ResponseHeaderRemoveTransform(string headerName, bool always)
        {
            HeaderName = headerName ?? throw new ArgumentNullException(nameof(headerName));
            Always = always;
        }

        internal string HeaderName { get; }

        internal bool Always { get; }

        // Assumes the response status code has been set on the HttpContext already.
        /// <inheritdoc/>
        public override ValueTask ApplyAsync(ResponseTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (Always || Success(context))
            {
                context.HttpContext.Response.Headers.Remove(HeaderName);
            }

            return default;
        }
    }
}
