// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ReverseProxy.Abstractions
{
    public sealed record ProxyHttpClientOptions : IEquatable<ProxyHttpClientOptions>
    {
        public SslProtocols? SslProtocols { get; init; }

        public bool? DangerousAcceptAnyServerCertificate { get; init; }

        public X509Certificate2 ClientCertificate { get; init; }

        public int? MaxConnectionsPerServer { get; init; }

        public bool? PropagateActivityContext { get; init; }

        // TODO: Add this property once we have migrated to SDK version that supports it.
        //public bool? EnableMultipleHttp2Connections { get; init; }

        public bool Equals(ProxyHttpClientOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return SslProtocols == other.SslProtocols
                && CertEquals(ClientCertificate, other.ClientCertificate)
                && DangerousAcceptAnyServerCertificate == other.DangerousAcceptAnyServerCertificate
                && MaxConnectionsPerServer == other.MaxConnectionsPerServer
                && PropagateActivityContext == other.PropagateActivityContext;
        }

        private static bool CertEquals(X509Certificate2 certificate1, X509Certificate2 certificate2)
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
