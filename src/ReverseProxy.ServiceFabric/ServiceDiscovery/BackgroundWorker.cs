// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <inheritdoc/>
    internal class BackgroundWorker : IHostedService
    {
        private readonly ILogger<BackgroundWorker> _logger;
        private readonly IMonotonicTimer _timer;
        private readonly IDiscoverer _serviceFabricDiscoveryWorker;
        private readonly ConfigProvider _configProvider;
        private readonly IOptionsMonitor<ServiceFabricDiscoveryOptions> _optionsMonitor;
        private Task _serviceFabricDiscoveryTask;
        private CancellationTokenSource _cts;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundWorker"/> class.
        /// </summary>
        public BackgroundWorker(
            ILogger<BackgroundWorker> logger,
            IMonotonicTimer timer,
            IDiscoverer serviceFabricDiscoveryWorker,
            ConfigProvider configProvider,
            IOptionsMonitor<ServiceFabricDiscoveryOptions> optionsMonitor)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _serviceFabricDiscoveryWorker = serviceFabricDiscoveryWorker ?? throw new ArgumentNullException(nameof(serviceFabricDiscoveryWorker));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellation)
        {
            _cts = new CancellationTokenSource();
            if (_serviceFabricDiscoveryTask != null)
            {
                _logger.LogError("Tried to start service fabric discovery loop and it was already running.");
            }
            else
            {
                _logger.LogInformation("Started service fabric discovery loop.");
                _serviceFabricDiscoveryTask = ServiceFabricDiscoveryLoop(_cts.Token);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken _)
        {
            if (_serviceFabricDiscoveryTask == null)
            {
                _logger.LogWarning("Service fabric discovery loop is not running.");
                return;
            }

            _logger.LogInformation("Stopping service fabric discovery loop.");
            _cts.Cancel();
            try
            {
                await _serviceFabricDiscoveryTask;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _serviceFabricDiscoveryTask = null;
            }
        }

        private async Task ServiceFabricDiscoveryLoop(CancellationToken cancellation)
        {
            var first = true;
            while (true)
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!first)
                    {
                        await _timer.Delay(_optionsMonitor.CurrentValue.DiscoveryPeriod, cancellation);
                    }

                    var result = await _serviceFabricDiscoveryWorker.DiscoverAsync(cancellation);
                    _configProvider.UpdateSnapshot(result.Routes, result.Clusters);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // Graceful shutdown
                    _logger.LogInformation("Service Fabric discovery loop is ending gracefully");
                    return;
                }
                catch (Exception ex) // TODO: davidni: not fatal?
                {
                    _logger.LogError(ex, "Swallowing unhandled exception from Service Fabric loop...");
                }

                first = false;
            }
        }
    }
}
