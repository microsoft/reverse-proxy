// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    /// <summary>
    /// Provides session affinity for load-balanced backends.
    /// </summary>
    internal interface ISessionAffinityProvider
    {
        /// <summary>
        /// Tries to find <see cref="DestinationInfo"/> to which the current request is affinitized by the affinity key.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="destinations"><see cref="DestinationInfo"/>s available for the request.</param>
        /// <param name="options">Affinity options.</param>
        /// <returns><see cref="AffinitizedDestinationCollection"/>.</returns>
        public AffinitizedDestinationCollection TryFindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, BackendConfig.BackendSessionAffinityOptions options);

        /// <summary>
        /// Affinitize the current request to the given <see cref="DestinationInfo"/> by setting the affinity key extracted from <see cref="DestinationInfo"/>.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="options">Affinity options.</param>
        /// <param name="destination"><see cref="DestinationInfo"/> to which request is to be affinitized.</param>
        public void AffinitizeRequest(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, DestinationInfo destination);

        /// <summary>
        /// Sets an affinity key on a downstream response if any is defined by <see cref="ISessionAffinityFeature"/>.
        /// </summary>
        /// <param name="context">Request context.</param>
        public void SetAffinityKeyOnDownstreamResponse(HttpContext context);
    }
}
