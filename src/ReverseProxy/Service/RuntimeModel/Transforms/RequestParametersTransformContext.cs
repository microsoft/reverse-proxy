// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transform state for use with <see cref="RequestParametersTransform"/>
    /// </summary>
    public class RequestParametersTransformContext
    {
        /// <summary>
        /// The current request context.
        /// </summary>
        public HttpContext HttpContext { get; set; }

        /// <summary>
        /// The HTTP version to use for the proxy request.
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        /// The HTTP method to use for the proxy request.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// The path to use for the proxy request.
        /// </summary>
        /// <remarks>
        /// This will be prefixed by any PathBase specified for the destination server.
        /// </remarks>
        public PathString Path { get; set; }

        /// <summary>
        /// The query used for the proxy request.
        /// </summary>
        public QueryTransformContext Query { get; set; }
    }
}
