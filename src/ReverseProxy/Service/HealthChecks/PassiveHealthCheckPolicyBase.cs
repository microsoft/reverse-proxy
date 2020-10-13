// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Base type for an <see cref="IPassiveHealthCheckPolicy"/> implementation.
    /// </summary>
    public abstract class PassiveHealthCheckPolicyBase : IPassiveHealthCheckPolicy
    {
        private static readonly TimeSpan _defaultReactivationPeriod = TimeSpan.FromMinutes(5);
        private readonly IReactivationScheduler _reactivationScheduler;

        protected PassiveHealthCheckPolicyBase(IReactivationScheduler reactivationScheduler)
        {
            _reactivationScheduler = reactivationScheduler ?? throw new ArgumentNullException(nameof(reactivationScheduler));
        }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public virtual void RequestProxied(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error)
        {
            var newHealth = error != null ? EvaluateFailedRequest(cluster, destination, context, error) : EvaluateSuccessfulRequest(cluster, destination, context);
            UpdateDestinationHealth(cluster, destination, newHealth);
        }

        /// <summary>
        /// Invoked for a failed request.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="destination">Destination the request was proxied to.</param>
        /// <param name="context">Request's <see cref="HttpContext"/>.</param>
        /// <param name="error">Detected error. It can be null.</param>
        /// <returns>New <see cref="DestinationHealth"/> value.</returns>
        protected abstract DestinationHealth EvaluateFailedRequest(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error);

        /// <summary>
        /// Invoked for a succeeded request.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="destination">Destination the request was proxied to.</param>
        /// <param name="context">Request's <see cref="HttpContext"/>.</param>
        /// <returns>New <see cref="DestinationHealth"/> value.</returns>
        protected abstract DestinationHealth EvaluateSuccessfulRequest(ClusterConfig cluster, DestinationInfo destination, HttpContext context);

        private void UpdateDestinationHealth(ClusterConfig cluster, DestinationInfo destination, DestinationHealth newPassiveHealth)
        {
            var state = destination.DynamicState;
            if (state == null)
            {
                destination.DynamicState = new DestinationDynamicState(new CompositeDestinationHealth(passive: newPassiveHealth, active: default));
            }
            else if (newPassiveHealth != state.Health.Passive)
            {
                destination.DynamicState = new DestinationDynamicState(state.Health.ChangePassive(newPassiveHealth));

                if (newPassiveHealth == DestinationHealth.Unhealthy)
                {
                    _reactivationScheduler.ScheduleRestoringAsHealthy(destination, cluster.HealthCheckOptions.Passive.ReactivationPeriod ?? _defaultReactivationPeriod);
                }
            }
        }
    }
}
