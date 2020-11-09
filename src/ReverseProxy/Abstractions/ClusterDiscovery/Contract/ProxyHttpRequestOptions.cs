using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Abstractions
{
    public sealed class ProxyHttpRequestOptions
    {
        public TimeSpan? RequestTimeout { get; set; }

        public Version Version { get; set; }

#if NET
        public HttpVersionPolicy? VersionPolicy { get; set; }
#endif

        internal ProxyHttpRequestOptions DeepClone()
        {
            return new ProxyHttpRequestOptions
            {
                RequestTimeout = RequestTimeout,
                Version = Version,
#if NET
                VersionPolicy = VersionPolicy,
#endif
            };
        }

        internal static bool Equals(ProxyHttpRequestOptions options1, ProxyHttpRequestOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.RequestTimeout == options2.RequestTimeout
                   && options1.Version == options2.Version
#if NET
                   && options1.VersionPolicy == options2.VersionPolicy
#endif
                ;
        }
    }
}
