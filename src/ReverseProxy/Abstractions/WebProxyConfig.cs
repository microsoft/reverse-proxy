using System;

namespace Yarp.ReverseProxy.Abstractions
{
    /// <summary>
    /// Config used to construct <seealso cref="System.Net.WebProxy"/> instance.
    /// </summary>
    public sealed record WebProxyConfig : IEquatable<WebProxyConfig>
    {
        /// <summary>
        /// The URI of the proxy server.
        /// </summary>
        public Uri Address { get; init; }

        /// <summary>
        /// true to bypass the proxy for local addresses; otherwise, false.
        /// If null, default value will be used: false
        /// </summary>
        public bool? BypassOnLocal { get; init; }

        /// <summary>
        /// Controls whether the <seealso cref="System.Net.CredentialCache.DefaultCredentials"/> are sent with requests.
        /// If null, default value will be used: false
        /// </summary>
        public bool? UseDefaultCredentials { get; init; }

        /// <inheritdoc/>
        public bool Equals(WebProxyConfig other)
        {
            if (other == null)
            {
                return false;
            }

            return Address == other.Address
                && BypassOnLocal == other.BypassOnLocal
                && UseDefaultCredentials == other.UseDefaultCredentials;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                Address,
                BypassOnLocal,
                UseDefaultCredentials
            );
        }
    }
}
