// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Converts <see cref="Cluster"/> and <see cref="Destination"/> metadata from the config format to the runtime one.
    /// </summary>
    public interface IMetadataConverter
    {
        /// <summary>
        /// Converts <see cref="Cluster"/>'s metadata.
        /// </summary>
        /// <param name="cluster"><see cref="Cluster"/></param>
        /// <returns>Runtime metadata.</returns>
        IReadOnlyDictionary<string, object> Convert(Cluster cluster);

        /// <summary>
        /// Converts <see cref="Destination"/>'s metadata.
        /// </summary>
        /// <param name="destination"><see cref="Destination"/></param>
        /// <returns>Runtime metadata.</returns>
        IReadOnlyDictionary<string, object> Convert(Destination destination);
    }
}
