using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Outgoing request configuration.
    /// </summary>
    public class ProxyHttpRequestData
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
        public string Version { get; set; }

#if NET
        /// <summary>
        /// Version policy for the outgoing request.
        /// Defines whether to upgrade or downgrade HTTP version if possible.
        /// Default is <c>RequestVersionOrLower</c>.
        /// </summary>
        public HttpVersionPolicy VersionPolicy { get; set; }
#endif
    }
}
