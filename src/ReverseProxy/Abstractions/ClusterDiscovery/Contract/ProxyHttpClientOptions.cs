// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ReverseProxy.Abstractions
{
    public sealed class ProxyHttpClientOptions
    {
        public SslProtocols? SslProtocols { get; set; }

        public bool? DangerousAcceptAnyServerCertificate { get; set; }

        public X509Certificate2 ClientCertificate { get; set; }

        public int? MaxConnectionsPerServer { get; set; }

        public bool? PropagateActivityContext { get; set; }

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
                MaxConnectionsPerServer = MaxConnectionsPerServer,
                PropagateActivityContext = PropagateActivityContext,
            };
        }

        internal static bool Equals(ProxyHttpClientOptions options1, ProxyHttpClientOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.SslProtocols == options2.SslProtocols
                && Equals(options1.ClientCertificate, options2.ClientCertificate)
                && options1.DangerousAcceptAnyServerCertificate == options2.DangerousAcceptAnyServerCertificate
                && options1.MaxConnectionsPerServer == options2.MaxConnectionsPerServer
                && options1.PropagateActivityContext == options2.PropagateActivityContext;
        }

        private static bool Equals(X509Certificate2 certificate1, X509Certificate2 certificate2)
        {
            if (certificate1 == null && certificate2 == null)
            {
                return true;
            }

            if (certificate1 == null || certificate2 == null)
            {
                return false;
            }

            return string.Equals(certificate1.Thumbprint, certificate2.Thumbprint, StringComparison.OrdinalIgnoreCase);
        }
    }
}
