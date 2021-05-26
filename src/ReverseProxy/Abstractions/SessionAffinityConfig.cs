// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Abstractions
{
    /// <summary>
    /// Session affinity options.
    /// </summary>
    public sealed record SessionAffinityConfig
    {

        public SessionAffinityConfig(string affinityKeyName)
        {
            AffinityKeyName = affinityKeyName;
        }

        /// <summary>
        /// Indicates whether session affinity is enabled.
        /// </summary>
        public bool? Enabled { get; init; }

        /// <summary>
        /// Session affinity mode which is implemented by one of providers.
        /// </summary>
        public string? Mode { get; init; }

        /// <summary>
        /// Strategy handling missing destination for an affinitized request.
        /// </summary>
        public string? FailurePolicy { get; init; }

        /// <summary>
        /// Identifies the name of the field where the affinity value is stored.
        /// For the cookie affinity provider this will be the cookie name.
        /// For the header affinity provider this will be the header name.
        /// The provider will give its own default if no value is set.
        /// This value should be unique across clusters to avoid affinity conflicts.
        /// https://github.com/microsoft/reverse-proxy/issues/976
        /// </summary>
        public string AffinityKeyName { get; init; }

        /// <summary>
        /// Configuration of a cookie storing the session affinity key in case
        /// the <see cref="Mode"/> is set to 'Cookie'.
        /// </summary>
        public SessionAffinityCookieConfig? Cookie { get; init; }

        /// <inheritdoc />
        public bool Equals(SessionAffinityConfig? other)
        {
            if (other == null)
            {
                return false;
            }

            return Enabled == other.Enabled
                && string.Equals(Mode, other.Mode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(FailurePolicy, other.FailurePolicy, StringComparison.OrdinalIgnoreCase)
                && string.Equals(AffinityKeyName, other.AffinityKeyName, StringComparison.Ordinal)
                && Cookie == other.Cookie;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Enabled,
                Mode?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                FailurePolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                AffinityKeyName?.GetHashCode(StringComparison.Ordinal),
                Cookie);
        }
    }
}
