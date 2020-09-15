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
    public sealed class Destination : IDeepCloneable<Destination>
    {
        /// <summary>
        /// Address of this destination. E.g. <c>https://127.0.0.1:123/abcd1234/</c>.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this destination.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <inheritdoc/>
        Destination IDeepCloneable<Destination>.DeepClone()
        {
            return new Destination
            {
                Address = Address,
                Metadata = Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase),
            };
        }

        internal static bool Equals(Destination destination1, Destination destination2)
        {
            if (destination1 == null && destination2 == null)
            {
                return true;
            }

            if (destination1 == null || destination2 == null)
            {
                return false;
            }

            return string.Equals(destination1.Address, destination2.Address, StringComparison.OrdinalIgnoreCase)
                && CaseInsensitiveEqualHelper.Equals(destination1.Metadata, destination2.Metadata);
        }
    }
}
