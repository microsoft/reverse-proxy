// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using System;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ReactivationScheduler : IReactivationScheduler, IDisposable
    {
        private readonly EntityActionScheduler<DestinationInfo> _scheduler;

        public ReactivationScheduler()
        {
            _scheduler = new EntityActionScheduler<DestinationInfo>(Reactivate, autoStart: true, runOnce: true);
        }

        public void Schedule(DestinationInfo destination, TimeSpan reactivationPeriod)
        {
            _scheduler.ScheduleEntity(destination, reactivationPeriod);
        }

        public void Dispose()
        {
            _scheduler.Dispose();
        }

        private void Reactivate(DestinationInfo destination)
        {
            var state = destination.DynamicState;
            if (state.Health.Passive == DestinationHealth.Unhealthy)
            {
                destination.DynamicState = new DestinationDynamicState(state.Health.ChangePassive(DestinationHealth.Unknown));
            }
        }
    }
}
