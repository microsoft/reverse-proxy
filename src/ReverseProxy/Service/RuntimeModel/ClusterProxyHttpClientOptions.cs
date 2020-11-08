// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public readonly struct ClusterProxyHttpClientOptions : IEquatable<ClusterProxyHttpClientOptions>
    {
        public ClusterProxyHttpClientOptions(
            SslProtocols? sslProtocols,
            bool acceptAnyServerCertificate,
            X509Certificate2 clientCertificate,
            int? maxConnectionsPerServer,
            bool? propagateActivityContext)
        {
            SslProtocols = sslProtocols;
            DangerousAcceptAnyServerCertificate = acceptAnyServerCertificate;
            ClientCertificate = clientCertificate;
            MaxConnectionsPerServer = maxConnectionsPerServer;
            PropagateActivityContext = propagateActivityContext;
        }

        public SslProtocols? SslProtocols { get; }

        public bool DangerousAcceptAnyServerCertificate { get; }

        public X509Certificate2 ClientCertificate { get; }

        public int? MaxConnectionsPerServer { get; }

        public bool? PropagateActivityContext { get; }

        // TODO: Add this property once we have migrated to SDK version that supports it.
        //public bool? EnableMultipleHttp2Connections { get; }

        public override bool Equals(object obj)
        {
            return obj is ClusterProxyHttpClientOptions options && Equals(options);
        }

        public bool Equals(ClusterProxyHttpClientOptions other)
        {
            return SslProtocols == other.SslProtocols &&
                   DangerousAcceptAnyServerCertificate == other.DangerousAcceptAnyServerCertificate &&
                   EqualityComparer<X509Certificate2>.Default.Equals(ClientCertificate, other.ClientCertificate) &&
                   MaxConnectionsPerServer == other.MaxConnectionsPerServer &&
                   PropagateActivityContext == other.PropagateActivityContext;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SslProtocols, DangerousAcceptAnyServerCertificate, ClientCertificate, MaxConnectionsPerServer);
        }

        public static bool operator ==(ClusterProxyHttpClientOptions left, ClusterProxyHttpClientOptions right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ClusterProxyHttpClientOptions left, ClusterProxyHttpClientOptions right)
        {
            return !(left == right);
        }
    }
}
