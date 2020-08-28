// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Proxy.Infrastructure
{
    public class ProxyHttpClientContext
    {
        public string ClusterID { get; }

        public ClusterConfig.ClusterProxyHttpClientOptions OldOptions { get; }

        public IReadOnlyDictionary<string, string> OldMetadata { get; }

        public HttpMessageInvoker OldClient { get; }

        public ClusterConfig.ClusterProxyHttpClientOptions NewOptions { get; }

        public IReadOnlyDictionary<string, string> NewMetadata { get; }

        public ProxyHttpClientContext(
            string clusterId,
            ClusterConfig.ClusterProxyHttpClientOptions oldOptions,
            IReadOnlyDictionary<string, string> oldMetadata,
            HttpMessageInvoker oldClient,
            ClusterConfig.ClusterProxyHttpClientOptions newOptions,
            IReadOnlyDictionary<string, string> newMetadata)
        {
            ClusterID = clusterId;
            OldOptions = oldOptions;
            OldMetadata = oldMetadata;
            OldClient = oldClient;
            NewOptions = newOptions;
            NewMetadata = newMetadata;
        }
    }
}
