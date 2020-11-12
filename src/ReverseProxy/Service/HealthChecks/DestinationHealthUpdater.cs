// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class DestinationHealthUpdater : IDestinationHealthUpdater, IDisposable
    {
        private readonly EntityActionScheduler<(ClusterInfo Cluster, DestinationInfo Destination)> _scheduler;
        private readonly ILogger<DestinationHealthUpdater> _logger;

        public DestinationHealthUpdater(ITimerFactory timerFactory, ILogger<DestinationHealthUpdater> logger)
        {
            _scheduler = new EntityActionScheduler<(ClusterInfo Cluster, DestinationInfo Destination)>(d => Reactivate(d.Cluster, d.Destination), autoStart: true, runOnce: true, timerFactory);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetActive(ClusterInfo cluster, IEnumerable<NewActiveDestinationHealth> newHealthPairs)
        {
            var changed = false;
            foreach (var newHealthPair in newHealthPairs)
            {
                var destination = newHealthPair.Destination;
                var newHealth = newHealthPair.NewActiveHealth;

                var healthState = destination.Health;
                if (newHealth != healthState.Active)
                {
                    healthState.Active = newHealth;
                    changed = true;
                    if (newHealth == DestinationHealth.Unhealthy)
                    {
                        Log.ActiveDestinationHealthStateIsSetToUnhealthy(_logger, destination.DestinationId, cluster.ClusterId);
                    }
                    else
                    {
                        Log.ActiveDestinationHealthStateIsSet(_logger, destination.DestinationId, cluster.ClusterId, newHealth);
                    }
                }
            }

            if (changed)
            {
                cluster.UpdateDynamicState();
            }
        }

        public void SetPassive(ClusterInfo cluster, DestinationInfo destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
        {
            _ = SetPassiveAsync(cluster, destination, newHealth, reactivationPeriod);
        }

        internal Task SetPassiveAsync(ClusterInfo cluster, DestinationInfo destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
        {
            var healthState = destination.Health;
            if (newHealth != healthState.Passive)
            {
                healthState.Passive = newHealth;
                ScheduleReactivation(cluster, destination, newHealth, reactivationPeriod);
                return Task.Factory.StartNew(c => ((ClusterInfo)c).UpdateDynamicState(), cluster, TaskCreationOptions.RunContinuationsAsynchronously);
            }
            return Task.CompletedTask;
        }

        private void ScheduleReactivation(ClusterInfo cluster, DestinationInfo destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
        {
            if (newHealth == DestinationHealth.Unhealthy)
            {
                _scheduler.ScheduleEntity((cluster, destination), reactivationPeriod);
                Log.UnhealthyDestinationIsScheduledForReactivation(_logger, destination.DestinationId, reactivationPeriod);
            }
        }

        public void Dispose()
        {
            _scheduler.Dispose();
        }

        private Task Reactivate(ClusterInfo cluster, DestinationInfo destination)
        {
            var healthState = destination.Health;
            if (healthState.Passive == DestinationHealth.Unhealthy)
            {
                healthState.Passive = DestinationHealth.Unknown;
                Log.PassiveDestinationHealthResetToUnkownState(_logger, destination.DestinationId);
                cluster.UpdateDynamicState();
            }

            return Task.CompletedTask;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, TimeSpan, Exception> _unhealthyDestinationIsScheduledForReactivation = LoggerMessage.Define<string, TimeSpan>(
                LogLevel.Information,
                EventIds.UnhealthyDestinationIsScheduledForReactivation,
                "Destination `{destinationId}` marked as 'unhealthy` by the passive health check is scheduled for a reactivation in `{reactivationPeriod}`.");

            private static readonly Action<ILogger, string, Exception> _passiveDestinationHealthResetToUnkownState = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.PassiveDestinationHealthResetToUnkownState,
                "Passive health state of the destination `{destinationId}` is reset to 'unknown`.");

            private static readonly Action<ILogger, string, string, Exception> _activeDestinationHealthStateIsSetToUnhealthy = LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                EventIds.ActiveDestinationHealthStateIsSetToUnhealthy,
                "Active health state of destination `{destinationId}` on cluster `{clusterId}` is set to 'unhealthy'.");

            private static readonly Action<ILogger, string, string, DestinationHealth, Exception> _activeDestinationHealthStateIsSet = LoggerMessage.Define<string, string, DestinationHealth>(
                LogLevel.Information,
                EventIds.ActiveDestinationHealthStateIsSet,
                "Active health state of destination `{destinationId}` on cluster `{clusterId}` is set to '{newHealthState}'.");

            public static void ActiveDestinationHealthStateIsSetToUnhealthy(ILogger logger, string destinationId, string clusterId)
            {
                _activeDestinationHealthStateIsSetToUnhealthy(logger, destinationId, clusterId, null);
            }

            public static void ActiveDestinationHealthStateIsSet(ILogger logger, string destinationId, string clusterId, DestinationHealth newHealthState)
            {
                _activeDestinationHealthStateIsSet(logger, destinationId, clusterId, newHealthState, null);
            }

            public static void UnhealthyDestinationIsScheduledForReactivation(ILogger logger, string destinationId, TimeSpan reactivationPeriod)
            {
                _unhealthyDestinationIsScheduledForReactivation(logger, destinationId, reactivationPeriod, null);
            }

            public static void PassiveDestinationHealthResetToUnkownState(ILogger logger, string destinationId)
            {
                _passiveDestinationHealthResetToUnkownState(logger, destinationId, null);
            }
        }
    }
}
