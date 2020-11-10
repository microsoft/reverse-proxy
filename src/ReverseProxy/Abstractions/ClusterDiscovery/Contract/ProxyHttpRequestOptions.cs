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
        /// Timeout for the outgoing request.
        /// Default is 100 seconds.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// HTTP version for the outgoing request.
        /// Default is HTTP/2.
        /// </summary>
        public Version Version { get; set; }

#if NET
        /// <summary>
        /// Version policy for the outgoing request.
        /// Defines whether to upgrade or downgrade HTTP version if possible.
        /// Default is <c>RequestVersionOrLower</c>.
        /// </summary>
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
