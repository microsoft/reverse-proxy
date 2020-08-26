// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Describes a destination of a cluster.
    /// </summary>
    public sealed class Destination : IDeepCloneable<Destination>
    {
        public static string DefaultProtocolVersion = HttpVersion.Http2.ToString();

        /// <summary>
        /// Address of this destination. E.g. <c>https://127.0.0.1:123/abcd1234/</c>.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// HTTP protocol version to call this <see cref="Destination"/>.
        /// </summary>
        public string ProtocolVersion { get; set; }

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
                ProtocolVersion = ProtocolVersion,
                Metadata = Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase),
            };
        }
    }
}
