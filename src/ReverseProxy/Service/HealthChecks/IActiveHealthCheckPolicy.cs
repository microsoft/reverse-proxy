// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Active health check evaulation policy.
    /// </summary>
    public interface IActiveHealthCheckPolicy
    {
        /// <summary>
        /// Policy's name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Anaylizes results of active health probes sent to destinations and calculates their new health states.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="probingResults">Destination probing results.</param>
        void ProbingCompleted(ClusterInfo cluster, IReadOnlyList<DestinationProbingResult> probingResults);
    }
}
