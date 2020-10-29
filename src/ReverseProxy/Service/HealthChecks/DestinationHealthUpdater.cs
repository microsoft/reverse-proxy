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
        private readonly ConcurrentDictionary<ClusterInfo, object> _activeSynchTokens = new ConcurrentDictionary<ClusterInfo, object>();

        public DestinationHealthUpdater(IReactivationScheduler reactivationScheduler, ILogger<DestinationHealthUpdater> logger)
        {
            _reactivationScheduler = reactivationScheduler ?? throw new ArgumentNullException(nameof(reactivationScheduler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SetActiveAsync(ClusterInfo cluster, IEnumerable<(DestinationInfo Destination, DestinationHealth NewHealth)> newHealthPairs)
        {
            var updateSyncToken = new object();
            if (!_activeSynchTokens.TryAdd(cluster, updateSyncToken))
            {
                // Don't allow concurrent updates.
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                if (!_activeSynchTokens.TryGetValue(cluster, out var currentToken))
                {
                    // Update operation was cancelled.
                    return;
                }

                try
                {
                    cluster.PauseHealthyDestinationUpdates();

                    foreach (var newHealthPair in newHealthPairs)
                    {
                        var destination = newHealthPair.Destination;
                        var newHealth = newHealthPair.NewHealth;

                        var state = destination.DynamicState;
                        if (newHealth != state.Health.Active)
                        {
                            destination.DynamicState = new DestinationDynamicState(state.Health.ChangeActive(newHealth));

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
                }
                finally
                {
                    cluster.ResumeHealthyDestinationUpdates();
                    _activeSynchTokens.TryRemove(cluster, out _);
                }
            });
        }

        public Task SetPassiveAsync(ClusterInfo cluster, DestinationInfo destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
        {
            return Task.Run(() =>
            {
                var state = destination.DynamicState;
                if (newHealth != state.Health.Passive)
                {
                    destination.DynamicState = new DestinationDynamicState(state.Health.ChangePassive(newHealth));

                    if (newHealth == DestinationHealth.Unhealthy)
                    {
                        _reactivationScheduler.Schedule(destination, reactivationPeriod);
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
