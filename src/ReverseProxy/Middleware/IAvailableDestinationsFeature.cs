// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Tracks proxy cluster destinations that are available to handle the current request.
    /// </summary>
    public interface IAvailableDestinationsFeature
    {
        /// <summary>
        /// Cluster destinations that can handle the current request.
        /// </summary>
        IReadOnlyList<DestinationInfo> Destinations { get; set; }
    }
}
