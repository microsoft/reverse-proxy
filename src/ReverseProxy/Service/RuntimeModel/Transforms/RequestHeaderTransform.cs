// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        /// <param name="values">The original header value(s).</param>
        /// <returns>The transformed result.</returns>
        public abstract StringValues Apply(HttpContext context, StringValues values);
    }
}
