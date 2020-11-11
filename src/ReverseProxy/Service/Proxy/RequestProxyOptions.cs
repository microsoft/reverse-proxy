// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;
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
        /// Outgoing HTTP request options.
        /// </summary>
        public ClusterProxyHttpRequestOptions RequestOptions { get; set; }
    }
}

