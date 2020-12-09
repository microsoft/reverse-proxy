// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public record HttpTransforms
    {
        public static readonly HttpTransforms Empty = new HttpTransforms();

        /// <summary>
        /// Indicates if all request headers should be copied to the outgoing HttpRequestMessage before invoking
        /// OnRequest. Disable this if you want to manually control which headers are copied.
        /// Note this copy excludes certain protocol headers like HTTP/2 pseudo headers (":authority").
        /// </summary>
        public bool CopyRequestHeaders { get; init; } = true;

        /// <summary>
        /// Indicates if all response headers should be copied from the received HttpResponseMessage before invoking
        /// OnResponse. Disable this if you want to manually control which headers are copied.
        /// Note this copy excludes certain protocol headers like `Transfer-Encoding: chunked`.
        /// </summary>
        public bool CopyResponseHeaders { get; init; } = true;

        /// <summary>
        /// Indicates if all response trailers should be copied from the received HttpResponseMessage before invoking
        /// OnResponseTrailers. Disable this if you want to manually control which headers are copied.
        /// </summary>
        public bool CopyResponseTrailers { get; init; } = true;

        /// <summary>
        /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage fields are
        /// initialized except RequestUri, which will be initialized after the callback if no value is provided.
        /// The string parameter represents the destination URI prefix that should be used when constructing the RequestUri.
        /// The headers will be copied before the callback if CopyRequestHeaders is enabled.
        /// </summary>
        public Func<HttpContext, HttpRequestMessage, string, Task> OnRequest { get; init; }

        /// <summary>
        /// A callback that is invoked when the proxied response is received. The status code and reason phrase will be copied
        /// to the HttpContext.Response before the callback is invoked, but may still be modified there. The headers will be
        /// copied to HttpContext.Response.Headers before the callback if CopyResponseHeaders is enabled.
        /// </summary>
        public Func<HttpContext, HttpResponseMessage, Task> OnResponse { get; init; }

        /// <summary>
        /// A callback that is invoked after the response body to modify trailers, if supported. The trailers will be
        /// copied to the HttpContext.Response before the callback if CopyResponseTrailers is enabled.
        /// </summary>
        public Func<HttpContext, HttpResponseMessage, Task> OnResponseTrailers { get; init; }
    }
}
