// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// A factory for creating <see cref="HttpRequestMessage"/>s for active health probes to be sent to destinations.
    /// </summary>
    public interface IProbingRequestFactory
    {
        /// <summary>
        /// Creates a probing request.
        /// </summary>
        /// <param name="clusterConfig">Cluster's config.</param>
        /// <param name="destination">Destination being probed.</param>
        /// <returns>Probing <see cref="HttpRequestMessage"/>.</returns>
        HttpRequestMessage CreateRequest(ClusterConfig clusterConfig, DestinationInfo destination);
    }
}
