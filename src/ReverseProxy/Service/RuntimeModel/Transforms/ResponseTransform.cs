// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transforms for response headers and trailers.
    /// </summary>
    public abstract class ResponseTransform
    {
        /// <summary>
        /// Transforms the given response.
        /// </summary>
        /// <param name="context">The current request context.</param>
        /// <param name="proxyResponse">The proxied response.</param>
        public abstract Task ApplyAsync(HttpContext context, HttpResponseMessage proxyResponse);
    }
}
