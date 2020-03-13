// <copyright file="HealthProbeWorker.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IslandGateway.Core.Service.Management;
using IslandGateway.Utilities;
using Microsoft.Extensions.Logging;

namespace IslandGateway.Core.Service.HealthProbe
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
                        _logger.LogInformation($"Health check work stopping prober for '{backend.BackendId}'.");
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
                    _logger.LogInformation($"Health check prober for backend: '{backend.BackendId}' is disabled because {reason}.");
                }

                // Step 4: New prober need to been created, start the new registered prober.
                if (createNewProber)
                {
                    // Start probing health for all endpoints(replica) in this backend(service).
                    var newProber = _backendProberFactory.CreateBackendProber(backend.BackendId, backendConfig, backend.EndpointManager);
                    _activeProbers.Add(backend.BackendId, newProber);
                    proberAdded++;
                    _logger.LogInformation($"Prober for backend '{backend.BackendId}' has created.");
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
                    _logger.LogInformation($"Health check work stopping prober for '{kvp.Key}'.");
                    stopTasks.Add(kvp.Value.StopAsync());
                    probersToRemove.Add(kvp.Key);
                }
            }

            // remove the probes for backends that were removed.
            probersToRemove.ForEach(p => _activeProbers.Remove(p));
            await Task.WhenAll(stopTasks);
            _logger.LogInformation($"Health check prober are updated. Added {proberAdded} probes, removed {probersToRemove.Count} probes. There are now {_activeProbers.Count} probes.");
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
            _logger.LogInformation($"Health check has gracefully shut down.");
        }
    }
}
