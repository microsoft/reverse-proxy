// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health;

internal sealed class DestinationHealthUpdater : IDestinationHealthUpdater, IDisposable
{
    private readonly EntityActionScheduler<(ClusterState Cluster, DestinationState Destination)> _scheduler;
    private readonly IClusterDestinationsUpdater _clusterUpdater;
    private readonly ILogger<DestinationHealthUpdater> _logger;

    public DestinationHealthUpdater(
        ITimerFactory timerFactory,
        IClusterDestinationsUpdater clusterDestinationsUpdater,
        ILogger<DestinationHealthUpdater> logger)
    {
        _scheduler = new EntityActionScheduler<(ClusterState Cluster, DestinationState Destination)>(d => Reactivate(d.Cluster, d.Destination), autoStart: true, runOnce: true, timerFactory);
        _clusterUpdater = clusterDestinationsUpdater ?? throw new ArgumentNullException(nameof(clusterDestinationsUpdater));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void SetActive(ClusterState cluster, IEnumerable<NewActiveDestinationHealth> newHealthPairs)
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
            _clusterUpdater.UpdateAvailableDestinations(cluster);
        }
    }

    public void SetPassive(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
    {
        _ = SetPassiveAsync(cluster, destination, newHealth, reactivationPeriod);
    }

    internal Task SetPassiveAsync(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
    {
        var healthState = destination.Health;
        if (newHealth != healthState.Passive)
        {
            healthState.Passive = newHealth;
            ScheduleReactivation(cluster, destination, newHealth, reactivationPeriod);
            return Task.Factory.StartNew(c => UpdateDestinations(c!), cluster, TaskCreationOptions.RunContinuationsAsynchronously);
        }
        return Task.CompletedTask;
    }

    private void UpdateDestinations(object cluster)
    {
        _clusterUpdater.UpdateAvailableDestinations((ClusterState)cluster);
    }

    private void ScheduleReactivation(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
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

    private Task Reactivate(ClusterState cluster, DestinationState destination)
    {
        var healthState = destination.Health;
        if (healthState.Passive == DestinationHealth.Unhealthy)
        {
            healthState.Passive = DestinationHealth.Unknown;
            Log.PassiveDestinationHealthResetToUnkownState(_logger, destination.DestinationId);
            _clusterUpdater.UpdateAvailableDestinations(cluster);
        }

        return Task.CompletedTask;
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, TimeSpan, Exception?> _unhealthyDestinationIsScheduledForReactivation = LoggerMessage.Define<string, TimeSpan>(
            LogLevel.Information,
            EventIds.UnhealthyDestinationIsScheduledForReactivation,
            "Destination `{destinationId}` marked as 'unhealthy` by the passive health check is scheduled for a reactivation in `{reactivationPeriod}`.");

        private static readonly Action<ILogger, string, Exception?> _passiveDestinationHealthResetToUnkownState = LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.PassiveDestinationHealthResetToUnkownState,
            "Passive health state of the destination `{destinationId}` is reset to 'unknown`.");

        private static readonly Action<ILogger, string, string, Exception?> _activeDestinationHealthStateIsSetToUnhealthy = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            EventIds.ActiveDestinationHealthStateIsSetToUnhealthy,
            "Active health state of destination `{destinationId}` on cluster `{clusterId}` is set to 'unhealthy'.");

        private static readonly Action<ILogger, string, string, DestinationHealth, Exception?> _activeDestinationHealthStateIsSet = LoggerMessage.Define<string, string, DestinationHealth>(
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
