// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Describes a destination of a cluster.
    /// </summary>
    public sealed class DestinationData
    {
        /// <summary>
        /// Address of this destination. E.g. <c>https://127.0.0.1:123/abcd1234/</c>.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this destination.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }
    }
}
