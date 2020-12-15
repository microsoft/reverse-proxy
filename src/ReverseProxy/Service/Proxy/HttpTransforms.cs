// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public class HttpTransforms
    {
        /// <summary>
        /// A default set of transforms that copies all request and response fields and headers, except for some
        /// protocol specific values.
        /// </summary>
        public static readonly HttpTransforms Default = new HttpTransforms();

        /// <summary>
        /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage fields are
        /// initialized except RequestUri, which will be initialized after the callback if no value is provided.
        /// The string parameter represents the destination URI prefix that should be used when constructing the RequestUri.
        /// The headers are copied by the base implementation, excluding some protocol headers like HTTP/2 pseudo headers (":authority").
        /// </summary>
        public virtual Task TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                var headerName = header.Key;
                var headerValue = header.Value;
                if (StringValues.IsNullOrEmpty(headerValue))
                {
                    continue;
                }

                // Filter out HTTP/2 pseudo headers like ":method" and ":path", those go into other fields.
                if (headerName.Length > 0 && headerName[0] == ':')
                {
                    continue;
                }

                RequestUtilities.AddHeader(proxyRequest, headerName, headerValue);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// A callback that is invoked when the proxied response is received. The status code and reason phrase will be copied
        /// to the HttpContext.Response before the callback is invoked, but may still be modified there. The headers will be
        /// copied to HttpContext.Response.Headers by the base implementation, excludes certain protocol headers like
        /// `Transfer-Encoding: chunked`.
        /// </summary>
        public virtual Task TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            var responseHeaders = httpContext.Response.Headers;
            CopyResponseHeaders(proxyResponse.Headers, responseHeaders);
            if (proxyResponse.Content != null)
            {
                CopyResponseHeaders(proxyResponse.Content.Headers, responseHeaders);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// A callback that is invoked after the response body to modify trailers, if supported. The trailers will be
        /// copied to the HttpContext.Response by the base implementation.
        /// </summary>
        public virtual Task TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
            // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
            var responseTrailersFeature = httpContext.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                // Note that trailers, if any, should already have been declared in Proxy's response
                // by virtue of us having proxied all response headers in step 6.
                CopyResponseHeaders(proxyResponse.TrailingHeaders, outgoingTrailers);
            }

            return Task.CompletedTask;
        }


        private static void CopyResponseHeaders(HttpHeaders source, IHeaderDictionary destination)
        {
            foreach (var header in source)
            {
                var headerName = header.Key;
                if (RequestUtilities.ResponseHeadersToSkip.Contains(headerName))
                {
                    continue;
                }
                destination.Append(headerName, header.Value.ToArray());
            }
        }
    }
}
