using System;

namespace Yarp.ReverseProxy.Abstractions
{
    /// <summary>
    /// Options used to construct <seealso cref="System.Net.WebProxy"/> instance.
    /// </summary>
    public sealed record WebProxyOptions : IEquatable<WebProxyOptions>
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

        public bool Equals(WebProxyOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return Address == other.Address
                && BypassOnLocal == other.BypassOnLocal
                && UseDefaultCredentials == other.UseDefaultCredentials;
        }

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
