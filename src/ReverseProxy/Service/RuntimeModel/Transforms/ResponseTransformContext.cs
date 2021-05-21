// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transform state for use with <see cref="ResponseTransform"/>
    /// </summary>
    public class ResponseTransformContext
    {
        /// <summary>
        /// The current request context.
        /// </summary>
        public HttpContext HttpContext { get; init; } = default!;

        /// <summary>
        /// The incoming proxy response.
        /// </summary>
        public HttpResponseMessage ProxyResponse { get; init; } = default!;

        /// <summary>
        /// Gets or sets if the response headers have been copied from the HttpResponseMessage and HttpContent
        /// to the HttpResponse. Transforms use this when searching for the current value of a header they
        /// should operate on.
        /// </summary>
        public bool HeadersCopied { get; set; }

        /// <summary>
        /// Set to true if the proxy should exclude the body and trailing headers when proxying this response.
        /// Defaults to false.
        /// </summary>
        public bool SuppressResponseBody { get; set; }
    }
}
