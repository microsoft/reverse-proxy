// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// The base class for request transforms.
    /// </summary>
    public abstract class RequestTransform
    {
        /// <summary>
        /// Transforms any of the available fields before building the outgoing request.
        /// </summary>
        public abstract void Apply(RequestTransformContext context);

        // Capture and remove the current value, including any prior transforms.
        protected internal static StringValues TakeHeader(RequestTransformContext context, string name)
        {
            var existingValues = StringValues.Empty;
            if (context.HeadersCopied)
            {
                if (context.ProxyRequest.Headers.TryGetValues(name, out var values))
                {
                    context.ProxyRequest.Headers.Remove(name);
                    existingValues = values.ToArray();
                }
                else if (context.ProxyRequest.Content?.Headers.TryGetValues(name, out values) ?? false)
                {
                    context.ProxyRequest.Content.Headers.Remove(name);
                    existingValues = values.ToArray();
                }
            }
            else
            {
                existingValues = context.HttpContext.Request.Headers[name];
            }

            return existingValues;
        }

        protected internal static void AddHeader(RequestTransformContext context, string name, StringValues values)
        {
            RequestUtilities.AddHeader(context.ProxyRequest, name, values);
        }
    }
}
