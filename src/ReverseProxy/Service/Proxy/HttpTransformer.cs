// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.Proxy
{
    public class HttpTransformer
    {
        /// <summary>
        /// A default set of transforms that copies all request and response fields and headers, except for some
        /// protocol specific values.
        /// </summary>
        public static readonly HttpTransformer Default = new HttpTransformer();

        /// <summary>
        /// Used to create derived instances.
        /// </summary>
        protected HttpTransformer() { }

        /// <summary>
        /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage fields are
        /// initialized except RequestUri, which will be initialized after the callback if no value is provided.
        /// See <see cref="RequestUtilities.MakeDestinationAddress(string, PathString, QueryString)"/> for constructing a custom request Uri.
        /// The string parameter represents the destination URI prefix that should be used when constructing the RequestUri.
        /// The headers are copied by the base implementation, excluding some protocol headers like HTTP/2 pseudo headers (":authority").
        /// </summary>
        /// <param name="httpContext">The incoming request.</param>
        /// <param name="proxyRequest">The outgoing proxy request.</param>
        /// <param name="destinationPrefix">The uri prefix for the selected destination server which can be used to create the RequestUri.</param>
        public virtual ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
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

            return default;
        }

        /// <summary>
        /// A callback that is invoked when the proxied response is received. The status code and reason phrase will be copied
        /// to the HttpContext.Response before the callback is invoked, but may still be modified there. The headers will be
        /// copied to HttpContext.Response.Headers by the base implementation, excludes certain protocol headers like
        /// `Transfer-Encoding: chunked`.
        /// </summary>
        /// <param name="httpContext">The incoming request.</param>
        /// <param name="proxyResponse">The response from the destination.</param>
        /// <returns>A bool indicating if the response should be proxied to the client or not.</returns>
        public virtual ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            var responseHeaders = httpContext.Response.Headers;
            CopyResponseHeaders(httpContext, proxyResponse.Headers, responseHeaders);
            if (proxyResponse.Content != null)
            {
                CopyResponseHeaders(httpContext, proxyResponse.Content.Headers, responseHeaders);
            }

            return new ValueTask<bool>(true);
        }

        /// <summary>
        /// A callback that is invoked after the response body to modify trailers, if supported. The trailers will be
        /// copied to the HttpContext.Response by the base implementation.
        /// </summary>
        /// <param name="httpContext">The incoming request.</param>
        /// <param name="proxyResponse">The response from the destination.</param>
        public virtual ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
            // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
            var responseTrailersFeature = httpContext.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                // Note that trailers, if any, should already have been declared in Proxy's response
                // by virtue of us having proxied all response headers in step 6.
                CopyResponseHeaders(httpContext, proxyResponse.TrailingHeaders, outgoingTrailers);
            }

            return default;
        }


        private static void CopyResponseHeaders(HttpContext httpContext, HttpHeaders source, IHeaderDictionary destination)
        {
            var isHttp2OrGreater = ProtocolHelper.IsHttp2OrGreater(httpContext.Request.Protocol);

            foreach (var header in source)
            {
                var headerName = header.Key;
                if (RequestUtilities.ShouldSkipResponseHeader(headerName, isHttp2OrGreater))
                {
                    continue;
                }

                Debug.Assert(header.Value is string[]);
                destination.Append(headerName, header.Value as string[] ?? header.Value.ToArray());
            }
        }
    }
}
