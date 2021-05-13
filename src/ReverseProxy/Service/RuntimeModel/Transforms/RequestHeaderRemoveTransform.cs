// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Removes a request header.
    /// </summary>
    public class RequestHeaderRemoveTransform : RequestTransform
    {
        public RequestHeaderRemoveTransform(string headerName)
        {
            HeaderName = headerName ?? throw new ArgumentNullException(nameof(headerName));
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
