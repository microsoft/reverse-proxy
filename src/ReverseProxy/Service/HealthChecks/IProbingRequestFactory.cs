// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Factory creating probing <see cref="HttpRequestMessage"/>s to be sent to destinations.
    /// </summary>
    public interface IProbingRequestFactory
    {
        /// <summary>
        /// Creates a probing request.
        /// </summary>
        /// <param name="clusterConfig">Cluster's config.</param>
        /// <param name="destination">Destination being probed.</param>
        /// <returns>Probing <see cref="HttpRequestMessage"/>.</returns>
        HttpRequestMessage GetRequest(ClusterConfig clusterConfig, DestinationInfo destination);
    }
}
