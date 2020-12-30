// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
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
        public abstract Task ApplyAsync(RequestTransformContext context);

        /// <summary>
        /// Gets the current header value by first checking the HttpRequestMessage,
        /// then the HttpContent, and falling back to the HttpContext only if
        /// <see cref="RequestTransformContext.HeadersCopied"/> is not set.
        /// This ordering allows multiple transforms to mutate the same header.
        /// </summary>
        /// <param name="name">The name of the header to take.</param>
        /// <returns>The requested header value, or StringValues.Empty if none.</returns>
        protected internal static StringValues TakeHeader(RequestTransformContext context, string name)
        {
            var existingValues = StringValues.Empty;
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
            else if (!context.HeadersCopied)
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
