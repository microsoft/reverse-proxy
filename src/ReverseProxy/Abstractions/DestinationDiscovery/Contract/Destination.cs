// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Describes a destination of a cluster.
    /// </summary>
    public sealed record Destination : IEquatable<Destination>
    {
        /// <summary>
        /// Address of this destination. E.g. <c>https://127.0.0.1:123/abcd1234/</c>.
        /// </summary>
        public string Address { get; init; }

        /// <summary>
        /// Endpoint accepting active health check probes. E.g. <c>http://127.0.0.1:1234/</c>.
        /// </summary>
        public string Health { get; init; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this destination.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; }

        /// <inheritdoc />
        public bool Equals(Destination other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Health, other.Health, StringComparison.OrdinalIgnoreCase)
                && CaseInsensitiveEqualHelper.Equals(Metadata, other.Metadata);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(
                Address?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                Health?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                Metadata);
        }
    }
}
