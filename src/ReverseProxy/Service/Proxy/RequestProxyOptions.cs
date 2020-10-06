// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public class RequestProxyOptions
    {
        public Transforms Transforms { get; set; } = Transforms.Empty;

        /// <summary>
        /// The time allowed to send the request and receive the response headers. This may include
        /// the time needed to send the request body.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

        // Future:
        // ResponseBodyTimeout - The time allowed to receive the full response body. Default to infinite. Not applied to Upgraded requests or gRPC streams.
        // HttpVersion - Default to HTTP/2?
        // HttpVersionPolicy - Default to OrLower?
    }
}
