// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Policy evaluating which destinations should be available for proxying requests to.
    /// </summary>
    public interface IAvailableDestinationsPolicy
    {
        /// <summary>
        /// Policy name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Reviews all given destinations and returns the ones available for proxying requests to.
        /// </summary>
        /// <param name="config">Target cluster.</param>
        /// <param name="allDestinations">All destinations configured for the target cluster.</param>
        /// <returns></returns>
        IReadOnlyList<DestinationState> GetAvailalableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations);
    }
}
