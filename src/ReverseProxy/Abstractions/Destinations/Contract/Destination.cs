// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

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
        /// Optionally, a different address to use for health probes against this endpoint. E.g. <c>https://127.0.0.1:234/</c>.
        /// If not specified, <see cref="Address" /> is used for both proxying and health probes.
        /// </summary>
        public string HealthAddress { get; set; }

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
                HealthAddress = HealthAddress,
                Metadata = Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase),
            };
        }
    }
}
