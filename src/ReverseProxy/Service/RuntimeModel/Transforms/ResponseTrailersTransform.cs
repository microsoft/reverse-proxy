// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transforms for response trailers.
    /// </summary>
    public abstract class ResponseTrailersTransform
    {
        /// <summary>
        /// Transforms the given response trailers. The trailers will have (optionally) already been
        /// copied to the <see cref="HttpResponse"/> and any changes should be made there.
        /// </summary>
        public abstract ValueTask ApplyAsync(ResponseTrailersTransformContext context);

        /// <summary>
        /// Removes and returns the current trailer value by first checking the HttpResponse
        /// and falling back to the value from HttpResponseMessage only if
        /// <see cref="ResponseTrailersTransformContext.HeadersCopied"/> is not set.
        /// This ordering allows multiple transforms to mutate the same header.
        /// </summary>
        /// <param name="headerName">The name of the header to take.</param>
        /// <returns>The response header value, or StringValues.Empty if none.</returns>
        public static StringValues TakeHeader(ResponseTrailersTransformContext context, string headerName)
        {
            var existingValues = StringValues.Empty;
            var responseTrailersFeature = context.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
            var responseTrailers = responseTrailersFeature.Trailers;
            // Support should have already been checked by the caller.
            Debug.Assert(responseTrailers != null);
            Debug.Assert(!responseTrailers.IsReadOnly);

            if (responseTrailers.TryGetValue(headerName, out var responseValues))
            {
                responseTrailers.Remove(headerName);
                existingValues = responseValues;
            }
            else if (!context.HeadersCopied
                && context.ProxyResponse.TrailingHeaders.TryGetValues(headerName, out var values))
            {
                existingValues = (string[])values;
            }

            return existingValues;
        }

        /// <summary>
        /// Sets the given trailer on the HttpResponse.
        /// </summary>
        public static void SetHeader(ResponseTrailersTransformContext context, string headerName, StringValues values)
        {
            var responseTrailersFeature = context.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
            var responseTrailers = responseTrailersFeature.Trailers;
            // Support should have already been checked by the caller.
            Debug.Assert(responseTrailers != null);
            Debug.Assert(!responseTrailers.IsReadOnly);

            responseTrailers[headerName] = values;
        }

        internal static bool Success(ResponseTrailersTransformContext context)
        {
            // TODO: How complex should this get? Compare with http://nginx.org/en/docs/http/ngx_http_headers_module.html#add_header
            return context.HttpContext.Response.StatusCode < 400;
        }
    }
}
