// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ReverseProxy.Abstractions
{
    public sealed class ProxyHttpClientOptions
    {
        public SslProtocols? SslProtocols { get; set; }

        public bool DangerousAcceptAnyServerCertificate { get; set; } = true;

        public X509Certificate2 ClientCertificate { get; set; }

        public int? MaxConnectionsPerServer { get; set; }

        // TODO: Add this property once we have migrated to SDK version that supports it.
        //public bool? EnableMultipleHttp2Connections { get; set; }

        internal ProxyHttpClientOptions DeepClone()
        {
            return new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols,
                DangerousAcceptAnyServerCertificate = DangerousAcceptAnyServerCertificate,
                // TODO: Clone certificate?
                ClientCertificate = ClientCertificate,
                MaxConnectionsPerServer = MaxConnectionsPerServer
            };
        }
    }
}
