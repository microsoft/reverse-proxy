// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple request header values.
    /// </summary>
    public class RequestCopyHostTransform : RequestTransform
    {
        public static RequestCopyHostTransform Instance = new();

        private RequestCopyHostTransform()
        {
        }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Always override the proxy request host with the original request host.
            context.ProxyRequest.Headers.Host = context.HttpContext.Request.Host.Value;
            return default;
        }
    }
}
