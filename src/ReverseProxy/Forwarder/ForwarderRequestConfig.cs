// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;

namespace Yarp.ReverseProxy.Forwarder
{
    /// <summary>
    /// Config for <see cref="IHttpForwarder.SendAsync"/>
    /// </summary>
    public sealed record ForwarderRequestConfig
    {
        /// <summary>
        /// An empty instance of this type.
        /// </summary>
        public static ForwarderRequestConfig Empty { get; } = new();

        /// <summary>
        /// The time allowed to send the request and receive the response headers. This may include
        /// the time needed to send the request body. The default is 100 seconds.
        /// </summary>
        public TimeSpan? Timeout { get; init; }

        /// <summary>
        /// Preferred version of the outgoing request.
        /// The default is HTTP/2.0.
        /// </summary>
        public Version? Version { get; init; }

#if NET
        /// <summary>
        /// The policy applied to version selection, e.g. whether to prefer downgrades, upgrades or
        /// request an exact version. The default is `RequestVersionOrLower`.
        /// </summary>
        public HttpVersionPolicy? VersionPolicy { get; init; }
#endif

        /// <summary>
        /// Allows to use write buffering when sending a response back to the client,
        /// if the server hosting YARP (e.g. IIS) supports it.
        /// NOTE: enabling it can break SSE (server side event) scenarios.
        /// </summary>
        public bool? AllowResponseBuffering { get; init; }

        /// <inheritdoc />
        public bool Equals(ForwarderRequestConfig? other)
        {
            if (other == null)
            {
                return false;
            }

            return Timeout == other.Timeout
#if NET
                && VersionPolicy == other.VersionPolicy
#endif
                && Version == other.Version
                && AllowResponseBuffering == other.AllowResponseBuffering;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Timeout,
#if NET
                VersionPolicy,
#endif
                Version,
                AllowResponseBuffering);
        }
    }
}

