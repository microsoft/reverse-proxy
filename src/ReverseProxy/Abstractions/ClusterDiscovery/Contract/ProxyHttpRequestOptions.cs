using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Outgoing request configuration.
    /// </summary>
    public sealed class ProxyHttpRequestOptions
    {
        /// <summary>
        /// The time allowed to send the request and receive the response headers. This may include
        /// the time needed to send the request body.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Preferred version of the outgoing request.
        /// </summary>
        public Version Version { get; set; }

#if NET
        /// <summary>
        /// The policy applied to version selection, e.g. whether to prefer downgrades, upgrades or
        /// request an exact version.
        /// </summary>
        public HttpVersionPolicy? VersionPolicy { get; set; }
#endif

        internal ProxyHttpRequestOptions DeepClone()
        {
            return new ProxyHttpRequestOptions
            {
                Timeout = Timeout,
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

            return options1.Timeout == options2.Timeout
                   && options1.Version == options2.Version
#if NET
                   && options1.VersionPolicy == options2.VersionPolicy
#endif
                ;
        }
    }
}
