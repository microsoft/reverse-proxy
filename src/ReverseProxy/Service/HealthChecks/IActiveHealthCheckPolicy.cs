// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
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
        /// Registers a successful or failed active health probing result and evaluates a new <see cref="CompositeDestinationHealth.Active"/> value.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="probingResults">Destination probing results.</param>
        void ProbingCompleted(ClusterInfo cluster, IReadOnlyList<DestinationProbingResult> probingResults);
    }
}
