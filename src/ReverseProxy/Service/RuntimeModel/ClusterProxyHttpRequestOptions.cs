using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public readonly struct ClusterProxyHttpRequestOptions
    {
        public ClusterProxyHttpRequestOptions(
            Version version
#if NET
            , HttpVersionPolicy versionPolicy
#endif
        )
        {
            Version = version;
#if NET
            VersionPolicy = versionPolicy;
#endif
        }

        public Version Version { get; }

#if NET
        public HttpVersionPolicy VersionPolicy { get; }
#endif
    }
}
