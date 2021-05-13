// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Yarp.ReverseProxy.Abstractions
{
    /// <summary>
    /// Options used for communicating with the destination servers.
    /// </summary>
    public sealed record HttpClientConfig
    {
        /// <summary>
        /// An empty options instance.
        /// </summary>
        public static readonly HttpClientConfig Empty = new();

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
        /// Specifies the activity correlation headers for outgoing requests.
        /// </summary>
        public ActivityContextHeaders? ActivityContextHeaders { get; init; }

        /// <summary>
        /// Optional web proxy used when communicating with the destination server. 
        /// </summary>
        public WebProxyConfig WebProxy { get; init; }

#if NET
        /// <summary>
        /// Gets or sets a value that indicates whether additional HTTP/2 connections can
        //  be established to the same server when the maximum number of concurrent streams
        //  is reached on all existing connections.
        /// </summary>
        public bool? EnableMultipleHttp2Connections { get; init; }

        /// <summary>
        /// Enables non-ASCII header encoding for outgoing requests.
        /// </summary>
        public string RequestHeaderEncoding { get; init; }
#endif

        /// <inheritdoc />
        public bool Equals(HttpClientConfig other)
        {
            if (other == null)
            {
                return false;
            }

            return SslProtocols == other.SslProtocols
                   && CertEquals(ClientCertificate, other.ClientCertificate)
                   && DangerousAcceptAnyServerCertificate == other.DangerousAcceptAnyServerCertificate
                   && MaxConnectionsPerServer == other.MaxConnectionsPerServer
#if NET
                   && EnableMultipleHttp2Connections == other.EnableMultipleHttp2Connections
                   // Comparing by reference is fine here since Encoding.GetEncoding returns the same instance for each encoding.
                   && RequestHeaderEncoding == other.RequestHeaderEncoding
#endif
                   && ActivityContextHeaders == other.ActivityContextHeaders
                   && WebProxy == other.WebProxy;
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
#if NET
                EnableMultipleHttp2Connections,
                RequestHeaderEncoding,
#endif
                ActivityContextHeaders,
                WebProxy);
        }
    }
}
