// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    /// <summary>
    /// Provides session affinity for load-balanced clusters.
    /// </summary>
    public interface ISessionAffinityProvider
    {
        /// <summary>
        ///  A unique identifier for this session affinity implementation. This will be referenced from config.
        /// </summary>
        string Mode { get; }

        /// <summary>
        /// Finds <see cref="DestinationInfo"/> to which the current request is affinitized by the affinity key.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="destinations"><see cref="DestinationInfo"/>s available for the request.</param>
        /// <param name="options">Cluster config.</param>
        /// <returns><see cref="AffinityResult"/> carrying the found affinitized destinations if any and the <see cref="AffinityStatus"/>.</returns>
        AffinityResult FindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, ClusterConfig clusterConfig);

        /// <summary>
        /// Affinitize the current request to the given <see cref="DestinationInfo"/> by setting the affinity key extracted from <see cref="DestinationInfo"/>.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="options">Cluster configuration.</param>
        /// <param name="destination"><see cref="DestinationInfo"/> to which request is to be affinitized.</param>
        void AffinitizeRequest(HttpContext context, ClusterConfig clusterConfig, DestinationInfo destination);
    }
}
