// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// The base class for request header transformations.
    /// </summary>
    public abstract class RequestHeaderTransform
    {
        /// <summary>
        /// Transforms the given value and returns the result.
        /// </summary>
        /// <param name="context">The original <see cref="HttpContext"/> for accessing other state.</param>
        /// <param name="proxyRequest">The outgoing <see cref="HttpRequestMessage"/> for reference.</param>
        /// <param name="values">The original header value(s) from the incoming request, or StringValues.Empty if the header
        /// was not present.</param>
        /// <returns>The transformed result, or StringValues.Empty if the header should be suppressed.</returns>
        public abstract StringValues Apply(HttpContext context, HttpRequestMessage proxyRequest, StringValues values);
    }
}
