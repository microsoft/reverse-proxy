// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a cluster
    /// that only change in reaction to configuration changes
    /// (e.g. health check options).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="ClusterConfig"/> are replaced
    /// in ther entirety when values need to change.
    /// </remarks>
    public sealed class ClusterConfig
    {
        public ClusterConfig(
            ClusterHealthCheckOptions healthCheckOptions,
            ClusterLoadBalancingOptions loadBalancingOptions,
            ClusterSessionAffinityOptions sessionAffinityOptions,
            HttpMessageInvoker httpClient,
            ClusterProxyHttpClientOptions httpClientOptions,
            IReadOnlyDictionary<string, string> metadata)
        {
            HealthCheckOptions = healthCheckOptions;
            LoadBalancingOptions = loadBalancingOptions;
            SessionAffinityOptions = sessionAffinityOptions;
            HttpClient = httpClient;
            HttpClientOptions = httpClientOptions;
            Metadata = metadata;
        }

        public ClusterHealthCheckOptions HealthCheckOptions { get; }

        public ClusterLoadBalancingOptions LoadBalancingOptions { get; }

        public ClusterSessionAffinityOptions SessionAffinityOptions { get; }

        public ClusterProxyHttpClientOptions HttpClientOptions { get; }

        /// <summary>
        /// An <see cref="HttpMessageInvoker"/> that used for proxying requests to an upstream server.
        /// </summary>
        public HttpMessageInvoker HttpClient { get; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this cluster.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Active health probing options for a cluster.
        /// </summary>
        /// <remarks>
        /// Struct used only to keep things organized as we add more configuration options inside of `ClusterConfig`.
        /// Each "feature" can have its own struct.
        /// </remarks>
        public readonly struct ClusterHealthCheckOptions
        {
            public ClusterHealthCheckOptions(bool enabled, TimeSpan interval, TimeSpan timeout, int port, string path)
            {
                Enabled = enabled;
                Interval = interval;
                Timeout = timeout;
                Port = port;
                Path = path;
            }

            /// <summary>
            /// Whether health probes are enabled.
            /// </summary>
            public bool Enabled { get; }

            /// <summary>
            /// Interval between health probes.
            /// </summary>
            public TimeSpan Interval { get; }

            /// <summary>
            /// Health probe timeout, after which the targeted endpoint is considered unhealthy.
            /// </summary>
            public TimeSpan Timeout { get; }

            /// <summary>
            /// Port number.
            /// </summary>
            public int Port { get; }

            /// <summary>
            /// Http path.
            /// </summary>
            public string Path { get; }
        }

        public readonly struct ClusterLoadBalancingOptions
        {
            public ClusterLoadBalancingOptions(LoadBalancingMode mode)
            {
                Mode = mode;
                // Increment returns the new value and we want the first return value to be 0.
                RoundRobinState = new AtomicCounter() { Value = -1 };
            }

            public LoadBalancingMode Mode { get; }

            internal AtomicCounter RoundRobinState { get; }
        }

        public readonly struct ClusterSessionAffinityOptions
        {
            public ClusterSessionAffinityOptions(bool enabled, string mode, string failurePolicy, IReadOnlyDictionary<string, string> settings)
            {
                Mode = mode;
                FailurePolicy = failurePolicy;
                Settings = settings;
                Enabled = enabled;
            }

            public bool Enabled { get; }

            public string Mode { get; }

            public string FailurePolicy { get; }

            public IReadOnlyDictionary<string, string> Settings { get;  }
        }

        public readonly struct ClusterProxyHttpClientOptions : IEquatable<ClusterProxyHttpClientOptions>
        {
            public ClusterProxyHttpClientOptions(
                SslProtocols? sslProtocols,
                bool acceptAnyServerCertificate,
                X509Certificate clientCertificate,
                int? maxConnectionsPerServer)
            {
                SslProtocols = sslProtocols;
                DangerousAcceptAnyServerCertificate = acceptAnyServerCertificate;
                ClientCertificate = clientCertificate;
                MaxConnectionsPerServer = maxConnectionsPerServer;
            }

            public SslProtocols? SslProtocols { get; }

            public bool DangerousAcceptAnyServerCertificate { get; }

            public X509Certificate ClientCertificate { get; }

            public int? MaxConnectionsPerServer { get; }

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
                       EqualityComparer<X509Certificate>.Default.Equals(ClientCertificate, other.ClientCertificate) &&
                       MaxConnectionsPerServer == other.MaxConnectionsPerServer;
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
}
