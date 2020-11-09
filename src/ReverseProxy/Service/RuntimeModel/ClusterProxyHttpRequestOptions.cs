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
#if NET
        public ClusterProxyHttpRequestOptions(TimeSpan? requestTimeout, Version version, HttpVersionPolicy? versionPolicy)
        {
            RequestTimeout = requestTimeout;
            Version = version;
            VersionPolicy = versionPolicy;
        }
#else
        public ClusterProxyHttpRequestOptions(TimeSpan? requestTimeout, Version version)
        {
            RequestTimeout = requestTimeout;
            Version = version;
        }
#endif

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
