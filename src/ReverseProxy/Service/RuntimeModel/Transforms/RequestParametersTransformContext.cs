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
        /// The outgoing proxy request. All field are initialized except for the 'RequestUri' and headers.
        /// If no value is provided then the 'RequestUri' will be initialized using the updated 'DestinationPrefix',
        /// 'Path', 'Query' properties after the transforms have run.
        /// The headers will be copied later when applying header transforms.
        /// </summary>
        public HttpRequestMessage Request { get; internal set; }

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

        /// <summary>
        /// The URI prefix for the proxy request. This includes the scheme and host and can optionally include a
        /// port and path base. The 'Path' and 'Query' properties will be appended to this after the transforms have run.
        /// </summary>
        public string DestinationPrefix { get; set; }
    }
}
