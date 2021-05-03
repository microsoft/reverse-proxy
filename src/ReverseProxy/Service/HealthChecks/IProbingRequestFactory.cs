// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// A factory for creating <see cref="HttpRequestMessage"/>s for active health probes to be sent to destinations.
    /// </summary>
    public interface IProbingRequestFactory
    {
        /// <summary>
        /// Creates a probing request.
        /// </summary>
        /// <param name="cluster">The cluster being probed.</param>
        /// <param name="destination">The destination being probed.</param>
        /// <returns>Probing <see cref="HttpRequestMessage"/>.</returns>
        HttpRequestMessage CreateRequest(ClusterModel cluster, DestinationConfig destination);
    }
}
