// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class DestinationHealthUpdater : IDestinationHealthUpdater
    {
        private readonly IReactivationScheduler _reactivationScheduler;
        private readonly ILogger<DestinationHealthUpdater> _logger;

        public DestinationHealthUpdater(IReactivationScheduler reactivationScheduler, ILogger<DestinationHealthUpdater> logger)
        {
            _reactivationScheduler = reactivationScheduler ?? throw new ArgumentNullException(nameof(reactivationScheduler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetActive(ClusterInfo cluster, IEnumerable<(DestinationInfo Destination, DestinationHealth NewHealth)> newHealthPairs)
        {
            var changed = false;
            foreach (var newHealthPair in newHealthPairs)
            {
                var destination = newHealthPair.Destination;
                var newHealth = newHealthPair.NewHealth;

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

        public Task SetPassiveAsync(ClusterInfo cluster, DestinationInfo destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
        {
            return Task.Run(() =>
            {
                var healthState = destination.Health;
                if (newHealth != healthState.Passive)
                {
                    healthState.Passive = newHealth;

                    cluster.UpdateDynamicState();

                    if (newHealth == DestinationHealth.Unhealthy)
                    {
                        _reactivationScheduler.Schedule(cluster, destination, reactivationPeriod);
                    }
                }
            });
        }

        private static class Log
        {
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
        }
    }
}
