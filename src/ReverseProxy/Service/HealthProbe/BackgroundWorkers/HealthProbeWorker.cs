// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    internal class HealthProbeWorker : IHealthProbeWorker
    {
        private static readonly int _maxProberNumber = 100;

        private readonly ILogger<HealthProbeWorker> _logger;
        private readonly IClusterManager _clusterManager;
        private readonly IClusterProberFactory _clusterProberFactory;
        private readonly AsyncSemaphore _semaphore = new AsyncSemaphore(_maxProberNumber);
        private readonly Dictionary<string, IClusterProber> _activeProbers = new Dictionary<string, IClusterProber>(StringComparer.Ordinal);

        public HealthProbeWorker(ILogger<HealthProbeWorker> logger, IClusterManager clusterManager, IClusterProberFactory clusterProberFactory)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(clusterManager, nameof(clusterManager));
            Contracts.CheckValue(clusterProberFactory, nameof(clusterProberFactory));

            _logger = logger;
            _clusterManager = clusterManager;
            _clusterProberFactory = clusterProberFactory;
        }

        public async Task UpdateTrackedClusters()
        {
            // TODO: (future) It's silly to go through all clusters just to see which ones changed, if any.
            // We should use Signals instead.

            // Step 1: Get the cluster table.
            var proberAdded = 0;
            var clusters = _clusterManager.GetItems();
            var clusterIdList = new HashSet<string>();

            // Step 2: Start background tasks to probe each cluster at their configured intervals. The number of concurrent probes are controlled by semaphore.
            var stopTasks = new List<Task>();
            foreach (var cluster in clusters)
            {
                clusterIdList.Add(cluster.ClusterId);
                var clusterConfig = cluster.Config.Value;
                var createNewProber = false;

                // Step 3A: Check whether this cluster already has a prober.
                if (_activeProbers.TryGetValue(cluster.ClusterId, out var activeProber))
                {
                    // Step 3B: Check if the config of cluster has changed.
                    if (clusterConfig == null ||
                        !clusterConfig.HealthCheckOptions.Enabled ||
                        activeProber.Config != clusterConfig)
                    {
                        // Current prober is not using latest config, stop and remove the outdated prober.
                        Log.HealthCheckStopping(_logger, cluster.ClusterId);
                        stopTasks.Add(activeProber.StopAsync());
                        _activeProbers.Remove(cluster.ClusterId);

                        // And create a new prober if needed
                        createNewProber = clusterConfig?.HealthCheckOptions.Enabled ?? false;
                    }
                }
                else
                {
                    // Step 3C: New cluster we aren't probing yet, set up a new prober.
                    createNewProber = clusterConfig?.HealthCheckOptions.Enabled ?? false;
                }

                if (!createNewProber)
                {
                    var reason = clusterConfig == null ? "no cluster configuration" : $"{nameof(clusterConfig.HealthCheckOptions)}.{nameof(clusterConfig.HealthCheckOptions.Enabled)} = false";
                    Log.HealthCheckDisabled(_logger, cluster.ClusterId, reason);
                }

                // Step 4: New prober need to been created, start the new registered prober.
                if (createNewProber)
                {
                    // Start probing health for all endpoints(replica) in this cluster(service).
                    var newProber = _clusterProberFactory.CreateClusterProber(cluster.ClusterId, clusterConfig, cluster.DestinationManager);
                    _activeProbers.Add(cluster.ClusterId, newProber);
                    proberAdded++;
                    Log.ProberCreated(_logger, cluster.ClusterId);
                    newProber.Start(_semaphore);
                }
            }

            // Step 5: Stop and remove probes for clusters that no longer exist.
            var probersToRemove = new List<string>();
            foreach (var kvp in _activeProbers)
            {
                if (!clusterIdList.Contains(kvp.Key))
                {
                    // Gracefully shut down the probe.
                    Log.HealthCheckStopping(_logger, kvp.Key);
                    stopTasks.Add(kvp.Value.StopAsync());
                    probersToRemove.Add(kvp.Key);
                }
            }

            // remove the probes for clusters that were removed.
            probersToRemove.ForEach(p => _activeProbers.Remove(p));
            await Task.WhenAll(stopTasks);
            Log.ProberUpdated(_logger, proberAdded, probersToRemove.Count, _activeProbers.Count);
        }

        public async Task StopAsync()
        {
            // Graceful shutdown of all probes.
            // Stop and remove probes for clusters that no longer exist
            var stopTasks = new List<Task>();
            foreach (var kvp in _activeProbers)
            {
                stopTasks.Add(kvp.Value.StopAsync());
            }

            await Task.WhenAll(stopTasks);
            Log.HealthCheckGracefulShutdown(_logger);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _healthCheckStopping = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.HealthCheckStopping,
                "Health check work stopping prober for '{clusterId}'.");

            private static readonly Action<ILogger, string, string, Exception> _healthCheckDisabled = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.HealthCheckDisabled,
                "Health check prober for cluster '{clusterId}' is disabled because {reason}.");

            private static readonly Action<ILogger, string, Exception> _proberCreated = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.ProberCreated,
                "Prober for cluster '{clusterId}' has created.");

            private static readonly Action<ILogger, int, int, int, Exception> _proberUpdated = LoggerMessage.Define<int, int, int>(
                LogLevel.Information,
                EventIds.ProberUpdated,
                "Health check prober are updated. " +
                "Added {addedProbers} probes, removed {removedProbers} probes. " +
                "There are now {activeProbers} probes.");

            private static readonly Action<ILogger, Exception> _healthCheckGracefulShutdown = LoggerMessage.Define(
                LogLevel.Information,
                EventIds.HealthCheckGracefulShutdown,
                "Health check has gracefully shut down.");

            public static void HealthCheckStopping(ILogger logger, string clusterId)
            {
                _healthCheckStopping(logger, clusterId, null);
            }

            public static void HealthCheckDisabled(ILogger logger, string clusterId, string reason)
            {
                _healthCheckDisabled(logger, clusterId, reason, null);
            }

            public static void ProberCreated(ILogger logger, string clusterId)
            {
                _proberCreated(logger, clusterId, null);
            }

            public static void ProberUpdated(ILogger logger, int addedProbers, int removedProbers, int activeProbers)
            {
                _proberUpdated(logger, addedProbers, removedProbers, activeProbers, null);
            }

            public static void HealthCheckGracefulShutdown(ILogger logger)
            {
                _healthCheckGracefulShutdown(logger, null);
            }
        }
    }
}
