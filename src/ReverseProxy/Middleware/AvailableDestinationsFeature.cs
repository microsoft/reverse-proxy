// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Tracks proxy destinations that are available to handle the current request.
    /// </summary>
    public class AvailableDestinationsFeature : IAvailableDestinationsFeature
    {
        /// <summary>
        /// Cluster destinations that can handle the current request.
        /// </summary>
        public IReadOnlyList<DestinationInfo> Destinations { get; set; }
    }
}
