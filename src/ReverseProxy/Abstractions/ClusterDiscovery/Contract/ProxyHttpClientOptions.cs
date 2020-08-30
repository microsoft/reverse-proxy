// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ReverseProxy.Abstractions
{
    public sealed class ProxyHttpClientOptions
    {
        public List<SslProtocols> SslProtocols { get; set; }

        public bool ValidateRemoteCertificate { get; set; } = true;

        public X509Certificate ClientCertificate { get; set; }

        public int? MaxConnectionsPerServer { get; set; }

        // TODO: Add this property once we have migrated to SDK version that supports it.
        //public bool? EnableMultipleHttp2Connections { get; set; }

        internal ProxyHttpClientOptions DeepClone()
        {
            return new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.CloneList(),
                ValidateRemoteCertificate = ValidateRemoteCertificate,
                // TODO: Clone certificate?
                ClientCertificate = ClientCertificate,
                MaxConnectionsPerServer = MaxConnectionsPerServer
            };
        }
    }
}
