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
        private readonly IBackendManager _backendManager;
        private readonly IBackendProberFactory _backendProberFactory;
        private readonly AsyncSemaphore _semaphore = new AsyncSemaphore(_maxProberNumber);
        private readonly Dictionary<string, IBackendProber> _activeProbers = new Dictionary<string, IBackendProber>(StringComparer.Ordinal);

        public HealthProbeWorker(ILogger<HealthProbeWorker> logger, IBackendManager backendManager, IBackendProberFactory backendProberFactory)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(backendManager, nameof(backendManager));
            Contracts.CheckValue(backendProberFactory, nameof(backendProberFactory));

            _logger = logger;
            _backendManager = backendManager;
            _backendProberFactory = backendProberFactory;
        }

        public async Task UpdateTrackedBackends()
        {
            // TODO: (future) It's silly to go through all backends just to see which ones changed, if any.
            // We should use Signals instead.

            // Step 1: Get the backend table.
            var proberAdded = 0;
            var backends = _backendManager.GetItems();
            var backendIdList = new HashSet<string>();

            // Step 2: Start background tasks to probe each backend at their configured intervals. The number of concurrent probes are controlled by semaphore.
            var stopTasks = new List<Task>();
            foreach (var backend in backends)
            {
                backendIdList.Add(backend.BackendId);
                var backendConfig = backend.Config.Value;
                var createNewProber = false;

                // Step 3A: Check whether this backend already has a prober.
                if (_activeProbers.TryGetValue(backend.BackendId, out var activeProber))
                {
                    // Step 3B: Check if the config of backend has changed.
                    if (backendConfig == null ||
                        !backendConfig.HealthCheckOptions.Enabled ||
                        activeProber.Config != backendConfig)
                    {
                        // Current prober is not using latest config, stop and remove the outdated prober.
                        Log.HealthCheckStopping(_logger, backend.BackendId);
                        stopTasks.Add(activeProber.StopAsync());
                        _activeProbers.Remove(backend.BackendId);

                        // And create a new prober if needed
                        createNewProber = backendConfig?.HealthCheckOptions.Enabled ?? false;
                    }
                }
                else
                {
                    // Step 3C: New backend we aren't probing yet, set up a new prober.
                    createNewProber = backendConfig?.HealthCheckOptions.Enabled ?? false;
                }

                if (!createNewProber)
                {
                    var reason = backendConfig == null ? "no backend configuration" : $"{nameof(backendConfig.HealthCheckOptions)}.{nameof(backendConfig.HealthCheckOptions.Enabled)} = false";
                    Log.HealthCheckDisabled(_logger, backend.BackendId, reason);
                }

                // Step 4: New prober need to been created, start the new registered prober.
                if (createNewProber)
                {
                    // Start probing health for all endpoints(replica) in this backend(service).
                    var newProber = _backendProberFactory.CreateBackendProber(backend.BackendId, backendConfig, backend.DestinationManager);
                    _activeProbers.Add(backend.BackendId, newProber);
                    proberAdded++;
                    Log.ProberCreated(_logger, backend.BackendId);
                    newProber.Start(_semaphore);
                }
            }

            // Step 5: Stop and remove probes for backends that no longer exist.
            var probersToRemove = new List<string>();
            foreach (var kvp in _activeProbers)
            {
                if (!backendIdList.Contains(kvp.Key))
                {
                    // Gracefully shut down the probe.
                    Log.HealthCheckStopping(_logger, kvp.Key);
                    stopTasks.Add(kvp.Value.StopAsync());
                    probersToRemove.Add(kvp.Key);
                }
            }

            // remove the probes for backends that were removed.
            probersToRemove.ForEach(p => _activeProbers.Remove(p));
            await Task.WhenAll(stopTasks);
            Log.ProberUpdated(_logger, proberAdded, probersToRemove.Count, _activeProbers.Count);
        }

        public async Task StopAsync()
        {
            // Graceful shutdown of all probes.
            // Stop and remove probes for backends that no longer exist
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
                "Health check work stopping prober for '{backendId}'.");

            private static readonly Action<ILogger, string, string, Exception> _healthCheckDisabled = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.HealthCheckDisabled,
                "Health check prober for backend '{backendId}' is disabled because {reason}.");

            private static readonly Action<ILogger, string, Exception> _proberCreated = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.ProberCreated,
                "Prober for backend '{backendId}' has created.");

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

            public static void HealthCheckStopping(ILogger logger, string backendId)
            {
                _healthCheckStopping(logger, backendId, null);
            }

            public static void HealthCheckDisabled(ILogger logger, string backendId, string reason)
            {
                _healthCheckDisabled(logger, backendId, reason, null);
            }

            public static void ProberCreated(ILogger logger, string backendId)
            {
                _proberCreated(logger, backendId, null);
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
