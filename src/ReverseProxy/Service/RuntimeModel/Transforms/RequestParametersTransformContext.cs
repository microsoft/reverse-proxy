// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
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

#if NET5_0
        /// <summary>
        /// The HTTP version policy to use for the proxy request.
        /// </summary>
        public HttpVersionPolicy VersionPolicy { get; set; }
#elif NETCOREAPP3_1
        // HttpVersionPolicy didn't exist in .NET Core 3.1 and there's no equivalent.
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif

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
