using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public readonly struct ClusterProxyHttpRequestOptions
    {
        public ClusterProxyHttpRequestOptions(
            TimeSpan requestTimeout,
            Version version
#if NET
            , HttpVersionPolicy versionPolicy
#endif
        )
        {
            RequestTimeout = requestTimeout;
            Version = version;
#if NET
            VersionPolicy = versionPolicy;
#endif
        }

        public TimeSpan RequestTimeout { get; }

        public Version Version { get; }

#if NET
        public HttpVersionPolicy VersionPolicy { get; }
#endif
    }
}
