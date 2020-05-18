// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class SessionAffinityFeature : ISessionAffinityFeature
    {
        /// <inheritdoc/>
        public string DestinationKey { get; set; }

        /// <inheritdoc/>
        public SessionAffinityMode Mode { get; set; }

        /// <inheritdoc/>
        public string CustomHeaderName { get; set; }
    }
}
