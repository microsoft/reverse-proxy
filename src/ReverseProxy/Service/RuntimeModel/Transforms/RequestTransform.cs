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
        public abstract ValueTask ApplyAsync(RequestTransformContext context);

        /// <summary>
        /// Removes and returns the current header value by first checking the HttpRequestMessage,
        /// then the HttpContent, and falling back to the HttpContext only if
        /// <see cref="RequestTransformContext.HeadersCopied"/> is not set.
        /// This ordering allows multiple transforms to mutate the same header.
        /// </summary>
        /// <param name="headerName">The name of the header to take.</param>
        /// <returns>The requested header value, or StringValues.Empty if none.</returns>
        public static StringValues TakeHeader(RequestTransformContext context, string headerName)
        {
            var existingValues = StringValues.Empty;
            if (context.ProxyRequest.Headers.TryGetValues(headerName, out var values))
            {
                context.ProxyRequest.Headers.Remove(headerName);
                existingValues = (string[])values;
            }
            else if (context.ProxyRequest.Content?.Headers.TryGetValues(headerName, out values) ?? false)
            {
                context.ProxyRequest.Content.Headers.Remove(headerName);
                existingValues = (string[])values;
            }
            else if (!context.HeadersCopied)
            {
                existingValues = context.HttpContext.Request.Headers[headerName];
            }

            return existingValues;
        }

        internal static void RemoveHeader(RequestTransformContext context, string headerName)
        {
            if (!context.ProxyRequest.Headers.Remove(headerName))
            {
                context.ProxyRequest.Content?.Headers.Remove(headerName);
            }
        }

        /// <summary>
        /// Adds the given header to the HttpRequestMessage or HttpContent where applicable.
        /// </summary>
        public static void AddHeader(RequestTransformContext context, string headerName, StringValues values)
        {
            RequestUtilities.AddHeader(context.ProxyRequest, headerName, values);
        }
    }
}
