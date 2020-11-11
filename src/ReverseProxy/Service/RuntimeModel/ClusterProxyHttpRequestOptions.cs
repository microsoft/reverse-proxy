using System;
using System.Net;
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
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);
        public static readonly Version DefaultVersion = HttpVersion.Version20;
#if NET
        public static readonly HttpVersionPolicy DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
#endif

#if NET
        public ClusterProxyHttpRequestOptions(TimeSpan? timeout, Version version, HttpVersionPolicy? versionPolicy)
        {
            _timeout = timeout;
            _version = version;
            _versionPolicy = versionPolicy;
        }
#endif

        public ClusterProxyHttpRequestOptions(TimeSpan? timeout, Version version)
        {
            _timeout = timeout;
            _version = version;
#if NET
            _versionPolicy = null;
#endif
        }

        private readonly TimeSpan? _timeout;
        /// <summary>
        /// The time allowed to send the request and receive the response headers. This may include
        /// the time needed to send the request body. The default is 100 seconds.
        /// </summary>
        public TimeSpan Timeout => _timeout ?? DefaultTimeout;

        private readonly Version _version;
        /// <summary>
        /// Preferred version of the outgoing request.
        /// The default is HTTP/2.0.
        /// </summary>
        public Version Version => _version ?? DefaultVersion;

#if NET
        private readonly HttpVersionPolicy? _versionPolicy;
        /// <summary>
        /// The policy applied to version selection, e.g. whether to prefer downgrades, upgrades or
        /// request an exact version. The default is `RequestVersionOrLower`.
        /// </summary>
        public HttpVersionPolicy VersionPolicy => _versionPolicy ?? DefaultVersionPolicy;
#endif
    }
}
