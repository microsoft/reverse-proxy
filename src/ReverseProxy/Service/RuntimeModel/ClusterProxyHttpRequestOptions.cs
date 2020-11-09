using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Outgoing request configuration.
    ///
    /// Readonly copy of <see cref="Microsoft.ReverseProxy.Abstractions.ProxyHttpRequestOptions"/>.
    /// </summary>
    public readonly struct ClusterProxyHttpRequestOptions
    {
        public ClusterProxyHttpRequestOptions(
            TimeSpan? requestTimeout,
            Version version
#if NET
            , HttpVersionPolicy? versionPolicy
#endif
        )
        {
            RequestTimeout = requestTimeout;
            Version = version;
#if NET
            VersionPolicy = versionPolicy;
#endif
        }

        /// <summary>
        /// Timeout for the outgoing request.
        /// Default is 100 seconds.
        /// </summary>
        public TimeSpan? RequestTimeout { get; }

        /// <summary>
        /// HTTP version for the outgoing request.
        /// Default is HTTP/2.
        /// </summary>
        public Version Version { get; }

#if NET
        /// <summary>
        /// Version policy for the outgoing request.
        /// Defines whether to upgrade or downgrade HTTP version if possible.
        /// Default is <c>RequestVersionOrLower</c>.
        /// </summary>
        public HttpVersionPolicy? VersionPolicy { get; }
#endif
    }
}
