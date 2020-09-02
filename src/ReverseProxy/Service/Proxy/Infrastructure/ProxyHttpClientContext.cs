// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Proxy.Infrastructure
{
    public class ProxyHttpClientContext
    {
        public string ClusterId { get; set; }

        public ClusterConfig.ClusterProxyHttpClientOptions OldOptions { get; set; }

        public IReadOnlyDictionary<string, string> OldMetadata { get; set; }

        public HttpMessageInvoker OldClient { get; set; }

        public ClusterConfig.ClusterProxyHttpClientOptions NewOptions { get; set; }

        public IReadOnlyDictionary<string, string> NewMetadata { get; set; }
    }
}
