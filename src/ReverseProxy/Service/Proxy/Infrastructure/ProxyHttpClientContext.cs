// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Proxy.Infrastructure
{
    /// <summary>
    /// Contains the old and the new proxy HTTP client configurations.
    /// </summary>
    public class ProxyHttpClientContext
    {
        /// <summary>
        /// Id of a <see cref="ClusterConfig"/> HTTP client belongs to.
        /// </summary>
        public string ClusterId { get; set; }

        /// <summary>
        /// Old <see cref="ClusterProxyHttpClientOptions"/> instance
        /// from which the <see cref="OldClient"/> was created.
        /// Can be null if a client is getting constructed for the first time.
        /// </summary>
        public ClusterProxyHttpClientOptions OldOptions { get; set; }

        /// <summary>
        /// Old metadata instance from which the <see cref="OldClient"/> was created.
        /// Can be null if a client is getting constructed for the first time.
        /// </summary>
        public IReadOnlyDictionary<string, string> OldMetadata { get; set; }

        /// <summary>
        /// Old <see cref="HttpMessageInvoker"/> instance.
        /// Can be null if a client is getting constructed for the first time.
        /// </summary>
        public HttpMessageInvoker OldClient { get; set; }

        /// <summary>
        /// New <see cref="ClusterProxyHttpClientOptions"/> instance
        /// specifying the settings for a new client.
        /// CANNOT be null.
        /// </summary>
        public ClusterProxyHttpClientOptions NewOptions { get; set; }

        /// <summary>
        /// New metadata instance used for a new client construction.
        /// Can be null.
        /// </summary>
        public IReadOnlyDictionary<string, string> NewMetadata { get; set; }
    }
}
