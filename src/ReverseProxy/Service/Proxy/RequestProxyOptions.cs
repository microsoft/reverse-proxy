// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Options for <see cref="IHttpProxy.ProxyAsync"/>
    /// </summary>
    public sealed record RequestProxyOptions
    {
        /// <summary>
        /// The time allowed to send the request and receive the response headers. This may include
        /// the time needed to send the request body. The default is 100 seconds.
        /// </summary>
        public TimeSpan? Timeout { get; init; }

        /// <summary>
        /// Preferred version of the outgoing request.
        /// The default is HTTP/2.0.
        /// </summary>
        public Version Version { get; init; }

#if NET
        /// <summary>
        /// The policy applied to version selection, e.g. whether to prefer downgrades, upgrades or
        /// request an exact version. The default is `RequestVersionOrLower`.
        /// </summary>
        public HttpVersionPolicy? VersionPolicy { get; init; }
#endif

        /// <inheritdoc />
        public bool Equals(RequestProxyOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return Timeout == other.Timeout
#if NET
                && VersionPolicy == other.VersionPolicy
#endif
                && Version == other.Version;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Timeout,
#if NET
                VersionPolicy,
#endif
                Version);
        }
    }
}

