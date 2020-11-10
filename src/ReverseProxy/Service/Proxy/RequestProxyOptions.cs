// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Options for <see cref="IHttpProxy.ProxyAsync"/>
    /// </summary>
    public class RequestProxyOptions
    {
        /// <summary>
        /// Optional transforms for modifying the request and response.
        /// </summary>
        public Transforms Transforms { get; set; } = Transforms.Empty;

        /// <summary>
        /// The time allowed to send the request and receive the response headers. This may include
        /// the time needed to send the request body. The default is 100 seconds.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Preferred version of the outgoing request.
        /// The default is HTTP/2.0.
        /// </summary>
        public Version Version { get; set; } = HttpVersion.Version20;

#if NET
        /// <summary>
        /// The policy applied to version selection, e.g. whether to prefer downgrades, upgrades or request an exact version.
        /// The default is `RequestVersionOrLower`.
        /// </summary>
        public HttpVersionPolicy VersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
#endif
    }
}

