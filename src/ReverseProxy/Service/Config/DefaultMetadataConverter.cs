// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Config;

namespace Microsoft.ReverseProxy.Service.Config
{
    /// <inheritdoc/>
    internal class DefaultMetadataConverter : IMetadataConverter
    {
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Convert(Cluster cluster)
        {
            return cluster.Metadata.ToDictionary(c => c.Key, c => (object)c.Value);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Convert(Destination destination)
        {
            return destination.Metadata.ToDictionary(c => c.Key, c => (object)c.Value);
        }
    }
}
