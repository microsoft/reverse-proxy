// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    /// <summary>
    /// Handles failures caused by a missing <see cref="DestinationInfo"/> for an affinizied request.
    /// </summary>
    internal interface IMissingDestinationHandler
    {
        /// <summary>
        ///  A unique identifier for this missing destionation handler implementation. This will be referenced from config.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Handles destination affinitization failure when no <see cref="DestinationInfo"/> was found for the given request's affinity key.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="affinityKey">Request's affinity key.</param>
        /// <param name="availableDestinations"><see cref="DestinationInfo"/>s available for the request.</param>
        /// <returns>List of <see cref="DestinationInfo"/> chosen to be affinitized to the request.</returns>
        public IReadOnlyList<DestinationInfo> Handle(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, object affinityKey, IReadOnlyList<DestinationInfo> availableDestinations);
    }
}
