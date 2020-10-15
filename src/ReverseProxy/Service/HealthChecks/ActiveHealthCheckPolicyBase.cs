// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Base type for an <see cref="IActiveHealthCheckPolicy"/> implementation.
    /// </summary>
    public abstract class ActiveHealthCheckPolicyBase : IActiveHealthCheckPolicy
    {
        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public void ProbingCompleted(ClusterInfo cluster, IReadOnlyList<DestinationProbingResult> probingResults)
        {
            var newHealths = new DestinationHealth[probingResults.Count];
            var clusterConfig = cluster.Config.Value;
            for (var i = 0; i < probingResults.Count; i++)
            {
                var response = probingResults[i].Response;
                var exception = probingResults[i].Exception;
                var destination = probingResults[i].Destination;
                newHealths[i] = response != null && response.IsSuccessStatusCode && exception == null
                    ? EvaluateSuccessfulProbe(clusterConfig, destination, response)
                    : EvaluateFailedProbe(clusterConfig, destination, response, exception);
            }
            UpdateDestinationsHealth(cluster, probingResults, newHealths);
        }

        /// <summary>
        /// Invoked for a failed probe.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="destination">Probed destination.</param>
        /// <param name="response">Response to the probe.</param>
        /// <param name="exception">Exception thrown during probing. It can be null.</param>
        /// <returns>New <see cref="DestinationHealth"/> value.</returns>
        protected abstract DestinationHealth EvaluateFailedProbe(ClusterConfig cluster, DestinationInfo destination, HttpResponseMessage response, Exception exception);

        /// <summary>
        /// Invoked for a succeeded probe.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="destination">Probed destination.</param>
        /// <param name="response">Response to the probe.</param>
        /// <returns>New <see cref="DestinationHealth"/> value.</returns>
        protected abstract DestinationHealth EvaluateSuccessfulProbe(ClusterConfig cluster, DestinationInfo destination, HttpResponseMessage response);

        private void UpdateDestinationsHealth(ClusterInfo cluster, IReadOnlyList<DestinationProbingResult> probingResults, IReadOnlyList<DestinationHealth> newActiveHealths)
        {
            cluster.PauseHealthyDestinationUpdates();

            for (var i = 0; i < probingResults.Count; i++)
            {
                var destination = probingResults[i].Destination;
                var state = destination.DynamicState;
                if (newActiveHealths[i] != state.Health.Active)
                {
                    destination.DynamicState = new DestinationDynamicState(state.Health.ChangeActive(newActiveHealths[i]));
                }
            }

            cluster.ResumeHealthyDestinationUpdates();
        }
    }
}
