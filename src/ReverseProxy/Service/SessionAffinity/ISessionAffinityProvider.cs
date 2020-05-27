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
    public interface ISessionAffinityProvider
    {
        /// <summary>
        ///  A unique identifier for this session affinity implementation. This will be referenced from config.
        /// </summary>
        public string Mode { get; }

        /// <summary>
        /// Tries to find <see cref="DestinationInfo"/> to which the current request is affinitized by the affinity key.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="destinations"><see cref="DestinationInfo"/>s available for the request.</param>
        /// <param name="backendId">Target backend ID.</param>
        /// <param name="options">Affinity options.</param>
        /// <param name="affinityResult">Affinitized <see cref="DestinationInfo"/>s found for the request.</param>
        /// <returns><see cref="true"/> if affinitized <see cref="DestinationInfo"/>s were successfully found, otherwise <see cref="false"/>.</returns>
        public bool TryFindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, string backendId, in BackendConfig.BackendSessionAffinityOptions options, out AffinityResult affinityResult);

        /// <summary>
        /// Affinitize the current request to the given <see cref="DestinationInfo"/> by setting the affinity key extracted from <see cref="DestinationInfo"/>.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="options">Affinity options.</param>
        /// <param name="destination"><see cref="DestinationInfo"/> to which request is to be affinitized.</param>
        public void AffinitizeRequest(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, DestinationInfo destination);
    }
}
