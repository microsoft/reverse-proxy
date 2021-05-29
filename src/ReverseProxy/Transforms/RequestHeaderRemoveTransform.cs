// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms
{
    /// <summary>
    /// Removes a request header.
    /// </summary>
    public class RequestHeaderRemoveTransform : RequestTransform
    {
        public RequestHeaderRemoveTransform(string headerName)
        {
            if (string.IsNullOrEmpty(headerName))
            {
                throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
            }

            HeaderName = headerName;
        }

        internal string HeaderName { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!context.ProxyRequest.Headers.Remove(HeaderName))
            {
                context.ProxyRequest.Content?.Headers.Remove(HeaderName);
            }

            return default;
        }
    }
}
