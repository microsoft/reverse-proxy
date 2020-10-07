// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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
        public void ProbingCompleted(ClusterConfig cluster, DestinationInfo destination, HttpResponseMessage response, Exception exception)
        {
            var newHealth = response.IsSuccessStatusCode && exception == null
                ? EvaluateSuccessfulProbe(cluster, destination, response)
                : EvaluateFailedProbe(cluster, destination, response, exception);
            UpdateDestinationHealth(destination, newHealth);
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

        private void UpdateDestinationHealth(DestinationInfo destination, DestinationHealth newActiveHealth)
        {
            var compositeHealth = destination.DynamicState.Health;
            if (newActiveHealth != compositeHealth.Active)
            {
                destination.DynamicStateSignal.Value = new DestinationDynamicState(compositeHealth.ChangeActive(newActiveHealth));
            }
        }
    }
}
