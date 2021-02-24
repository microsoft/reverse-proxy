// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Options used for communicating with the destination servers.
    /// </summary>
    public sealed record ProxyHttpClientOptions
    {
        /// <summary>
        /// An empty options instance.
        /// </summary>
        public static readonly ProxyHttpClientOptions Empty = new();

        /// <summary>
        /// What TLS protocols to use.
        /// </summary>
        public SslProtocols? SslProtocols { get; init; }

        /// <summary>
        /// Indicates if destination server https certificate errors should be ignored.
        /// This should only be done when using self-signed certificates.
        /// </summary>
        public bool? DangerousAcceptAnyServerCertificate { get; init; }

        /// <summary>
        /// A client certificate used to authenticate to the destination server.
        /// </summary>
        public X509Certificate2 ClientCertificate { get; init; }

        /// <summary>
        /// Limits the number of connections used when communicating with the destination server.
        /// </summary>
        public int? MaxConnectionsPerServer { get; init; }

        /// <summary>
        /// Enables or disables the activity correlation headers for outgoing requests.
        /// </summary>
        public bool? PropagateActivityContext { get; init; }

        // TODO: Add this property once we have migrated to SDK version that supports it.
        //public bool? EnableMultipleHttp2Connections { get; init; }

#if NET
        /// <summary>
        /// Enables non-ASCII header encoding for outgoing requests.
        /// </summary>
        public Encoding RequestHeaderEncoding { get; init; }
#endif

        /// <inheritdoc />
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
                   && PropagateActivityContext == other.PropagateActivityContext
#if NET
                   // Comparing by reference is fine here since Encoding.GetEncoding returns the same instance for each encoding.
                   && RequestHeaderEncoding == other.RequestHeaderEncoding
#endif
                ;
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

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(SslProtocols,
                ClientCertificate?.Thumbprint,
                DangerousAcceptAnyServerCertificate,
                MaxConnectionsPerServer,
                PropagateActivityContext
#if NET
                , RequestHeaderEncoding
#endif
                );
        }
    }
}
