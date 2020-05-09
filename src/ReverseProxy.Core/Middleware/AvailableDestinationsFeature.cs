// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Middleware
{
    /// <summary>
    /// Tracks proxy destinations that are available to handle the current request.
    /// </summary>
    public class AvailableDestinationsFeature : IAvailableDestinationsFeature
    {
        /// <summary>
        /// Backend destinations that can handle the current request.
        /// </summary>
        public IReadOnlyList<DestinationInfo> Destinations { get; set; }
    }
}
