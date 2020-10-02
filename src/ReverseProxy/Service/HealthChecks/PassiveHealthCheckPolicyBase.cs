// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public abstract class PassiveHealthCheckPolicyBase : IPassiveHealthCheckPolicy
    {
        private static readonly TimeSpan _defaultReactivationPeriod = TimeSpan.FromMinutes(5);
        private readonly IReactivationScheduler _reactivationScheduler;

        protected PassiveHealthCheckPolicyBase(IReactivationScheduler reactivationScheduler)
        {
            _reactivationScheduler = reactivationScheduler ?? throw new ArgumentNullException(nameof(reactivationScheduler));
        }

        public abstract string Name { get; }

        public virtual void RequestProxied(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error)
        {
            var newHealth = error != null ? EvaluateFailedRequest(cluster, destination, context, error) : EvaluateSuccessfulRequest(cluster, destination, context);
            UpdateDestinationHealthState(cluster, destination, newHealth);
        }

        protected abstract DestinationHealth EvaluateFailedRequest(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error);

        protected abstract DestinationHealth EvaluateSuccessfulRequest(ClusterConfig cluster, DestinationInfo destination, HttpContext context);

        private void UpdateDestinationHealthState(ClusterConfig cluster, DestinationInfo destination, DestinationHealth newPassiveHealth)
        {
            var compositeHealth = destination.DynamicState.Health;
            if (newPassiveHealth != compositeHealth.Passive)
            {
                destination.DynamicStateSignal.Value = new DestinationDynamicState(compositeHealth.ChangePassive(newPassiveHealth));

                if (newPassiveHealth == DestinationHealth.Unhealthy)
                {
                    _reactivationScheduler.ScheduleRestoringAsHealthy(destination, cluster.HealthCheckOptions.Passive.ReactivationPeriod ?? _defaultReactivationPeriod);
                }
            }
        }
    }
}
