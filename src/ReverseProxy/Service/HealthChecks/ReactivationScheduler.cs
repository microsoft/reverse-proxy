// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;
using System;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ReactivationScheduler : IReactivationScheduler
    {
        private readonly EntityActionScheduler<DestinationInfo> _scheduler;

        public ReactivationScheduler(IUptimeClock clock)
        {
            _scheduler = new EntityActionScheduler<DestinationInfo>(Reactivate, runOnce: true, clock);
        }

        public void ScheduleRestoringAsHealthy(DestinationInfo destination, TimeSpan reactivationPeriod)
        {
            _scheduler.ScheduleEntity(destination, reactivationPeriod);
        }

        public void Dispose()
        {
            _scheduler.Dispose();
        }

        private void Reactivate(DestinationInfo destination)
        {
            destination.DynamicState = new DestinationDynamicState(destination.DynamicState.Health.ChangePassive(DestinationHealth.Unknown));
        }
    }
}
