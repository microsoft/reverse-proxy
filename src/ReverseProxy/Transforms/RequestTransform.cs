// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Proxy;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms
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
            if (string.IsNullOrEmpty(headerName))
            {
                throw new System.ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
            }

#if NET6_0_OR_GREATER
            HeaderStringValues existingValues = default;
            if (context.ProxyRequest.Headers.NonValidated.TryGetValues(headerName, out var values))
            {
                context.ProxyRequest.Headers.Remove(headerName);
                existingValues = values;
            }
            else if (context.ProxyRequest.Content?.Headers.NonValidated.TryGetValues(headerName, out values) ?? false)
            {
                context.ProxyRequest.Content.Headers.Remove(headerName);
                existingValues = values!;
            }
            else if (!context.HeadersCopied)
            {
                return context.HttpContext.Request.Headers[headerName];
            }

            var result = StringValues.Empty;
            foreach (var headerValue in existingValues)
            {
                result = StringValues.Concat(headerValue, result);
            }
            return result;
#else
            var existingValues = StringValues.Empty;
            if (context.ProxyRequest.Headers.TryGetValues(headerName, out var values))
            {
                context.ProxyRequest.Headers.Remove(headerName);
                existingValues = (string[])values;
            }
            else if (context.ProxyRequest.Content?.Headers.TryGetValues(headerName, out values) ?? false)
            {
                context.ProxyRequest.Content.Headers.Remove(headerName);
                existingValues = (string[])values!;
            }
            else if (!context.HeadersCopied)
            {
                existingValues = context.HttpContext.Request.Headers[headerName];
            }
            return existingValues;
#endif
        }

        /// <summary>
        /// Adds the given header to the HttpRequestMessage or HttpContent where applicable.
        /// </summary>
        public static void AddHeader(RequestTransformContext context, string headerName, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(headerName))
            {
                throw new System.ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
            }

            RequestUtilities.AddHeader(context.ProxyRequest, headerName, values);
        }
    }
}
