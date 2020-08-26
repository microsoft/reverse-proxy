// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract
{
    public sealed class ProxyHttpClientOptions : IDeepCloneable<ProxyHttpClientOptions>
    {
        public List<string> SslApplicationProtocols { get; set; }

        public X509RevocationMode RevocationMode { get; set; }

        public List<string> CipherSuitesPolicy { get; set; }

        public List<string> SslProtocols { get; set; }

        public EncryptionPolicy EncryptionPolicy { get; set; }

        public int MaxConnectionsPerServer {get; set;}

        public bool EnableMultipleHttp2Connections { get; set; }

        ProxyHttpClientOptions IDeepCloneable<ProxyHttpClientOptions>.DeepClone()
        {
            return new ProxyHttpClientOptions
            {
                SslApplicationProtocols = SslApplicationProtocols.DeepClone(),
                RevocationMode = RevocationMode,
                CipherSuitesPolicy = CipherSuitesPolicy.DeepClone(),
                SslProtocols = SslProtocols.DeepClone(),
                EncryptionPolicy = EncryptionPolicy,
                MaxConnectionsPerServer = MaxConnectionsPerServer,
                EnableMultipleHttp2Connections = EnableMultipleHttp2Connections
            };
        }
    }
}
