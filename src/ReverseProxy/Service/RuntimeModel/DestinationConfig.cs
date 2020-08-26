// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a destination
    /// that only change in reaction to configuration changes
    /// (e.g. address).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="DestinationConfig"/> are replaced
    /// in ther entirety when values need to change.
    /// </remarks>
    public sealed class DestinationConfig
    {
        public DestinationConfig(string address, string protocolVersion, IReadOnlyDictionary<string, object> metadata)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException(nameof(address));
            }
            if (string.IsNullOrEmpty(protocolVersion))
            {
                throw new ArgumentNullException(nameof(protocolVersion));
            }

            Address = address;
            ProtocolVersion = protocolVersion;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        // TODO: Make this a Uri.
        public string Address { get; }

        public string ProtocolVersion { get; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this destination.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; }
    }
}
