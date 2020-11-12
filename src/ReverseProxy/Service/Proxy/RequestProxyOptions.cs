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
    public readonly struct RequestProxyOptions
    {
        public RequestProxyOptions(Transforms transforms, ClusterProxyHttpRequestOptions requestOptions = default)
        {
            _transforms = transforms;
            RequestOptions = requestOptions;
        }

#if NET
        public RequestProxyOptions(TimeSpan? timeout, Version version, HttpVersionPolicy? versionPolicy)
            : this(null, new ClusterProxyHttpRequestOptions(timeout, version, versionPolicy))
        { }
#endif

        public RequestProxyOptions(TimeSpan? timeout, Version version)
            : this(null, new ClusterProxyHttpRequestOptions(timeout, version))
        { }

        private readonly Transforms _transforms;
        /// <summary>
        /// Optional transforms for modifying the request and response.
        /// </summary>
        public Transforms Transforms => _transforms ?? Transforms.Empty;

        /// <summary>
        /// Outgoing HTTP request options.
        /// </summary>
        public ClusterProxyHttpRequestOptions RequestOptions { get; }
    }
}

