// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <inheritdoc/>
    internal class ServiceFabricServiceDiscovery : IServiceDiscovery
    {
        private readonly ILogger<ServiceFabricServiceDiscovery> _logger;
        private readonly IMonotonicTimer _timer;
        private readonly IReverseProxyConfigManager _proxyManager;
        private readonly IServiceFabricDiscoveryWorker _serviceFabricDiscoveryWorker;
        private Task _serviceFabricDiscoveryTask;
        private CancellationTokenSource _cts;
        private ServiceFabricServiceDiscoveryOptions _options = new ServiceFabricServiceDiscoveryOptions();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceFabricServiceDiscovery"/> class.
        /// </summary>
        public ServiceFabricServiceDiscovery(
            ILogger<ServiceFabricServiceDiscovery> logger,
            IMonotonicTimer timer,
            IReverseProxyConfigManager proxyManager,
            IServiceFabricDiscoveryWorker serviceFabricDiscoveryWorker)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(proxyManager, nameof(proxyManager));
            Contracts.CheckValue(serviceFabricDiscoveryWorker, nameof(serviceFabricDiscoveryWorker));

            _logger = logger;
            _timer = timer;
            _proxyManager = proxyManager;
            _serviceFabricDiscoveryWorker = serviceFabricDiscoveryWorker;
        }

        /// <inheritdoc/>
        public string Name { get; } = "servicefabric";

        /// <inheritdoc/>
        public Task SetConfigAsync(IConfigurationSection newConfig, CancellationToken _)
        {
            var newOptions = new ServiceFabricServiceDiscoveryOptions();
            newConfig.Bind(newOptions);
            _options = newOptions;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Start()
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
            var errorReporter = new LoggerConfigErrorReporter(_logger);
            while (true)
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    await _serviceFabricDiscoveryWorker.ExecuteAsync(_options, cancellation);
                    await _proxyManager.ApplyConfigurationsAsync(errorReporter, cancellation);
                    await _timer.Delay(_options.DiscoveryPeriod, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // Graceful shutdown
                    _logger.LogInformation("Service Fabric discovery loop is ending gracefully");
                    return;
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    _logger.LogError(ex, "Swallowing unhandled exception from service Fabric loop...");
                }
            }
        }

        private class LoggerConfigErrorReporter : IConfigErrorReporter
        {
            private readonly ILogger _logger;

            public LoggerConfigErrorReporter(ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public void ReportError(string code, string itemId, string message)
            {
                _logger.LogWarning($"Config error: {message}, {code}, {itemId}.");
            }

            public void ReportError(string code, string itemId, string message, Exception ex)
            {
                _logger.LogWarning($"Failed to apply new configs: {message}, {code}, {itemId}, {ex}.");
            }
        }
    }
}
