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
        private readonly ILogger<ServiceFabricServiceDiscovery> logger;
        private readonly IMonotonicTimer timer;
        private readonly IReverseProxyConfigManager gatewayManager;
        private readonly IServiceFabricDiscoveryWorker serviceFabricDiscoveryWorker;
        private Task serviceFabricDiscoveryTask = null;
        private CancellationTokenSource cts;
        private ServiceFabricServiceDiscoveryOptions options = new ServiceFabricServiceDiscoveryOptions();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceFabricServiceDiscovery"/> class.
        /// </summary>
        public ServiceFabricServiceDiscovery(
            ILogger<ServiceFabricServiceDiscovery> logger,
            IMonotonicTimer timer,
            IReverseProxyConfigManager gatewayManager,
            IServiceFabricDiscoveryWorker serviceFabricDiscoveryWorker)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(gatewayManager, nameof(gatewayManager));
            Contracts.CheckValue(serviceFabricDiscoveryWorker, nameof(serviceFabricDiscoveryWorker));

            this.logger = logger;
            this.timer = timer;
            this.gatewayManager = gatewayManager;
            this.serviceFabricDiscoveryWorker = serviceFabricDiscoveryWorker;
        }

        /// <inheritdoc/>
        public string Name { get; } = "servicefabric";

        /// <inheritdoc/>
        public Task SetConfigAsync(IConfigurationSection newConfig, CancellationToken _)
        {
            var newOptions = new ServiceFabricServiceDiscoveryOptions();
            newConfig.Bind(newOptions);
            this.options = newOptions;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Start()
        {
            this.cts = new CancellationTokenSource();
            if (this.serviceFabricDiscoveryTask != null)
            {
                this.logger.LogError("Tried to start service fabric discovery loop and it was already running.");
            }
            else
            {
                this.logger.LogInformation("Started service fabric discovery loop.");
                this.serviceFabricDiscoveryTask = this.ServiceFabricDiscoveryLoop(this.cts.Token);
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken _)
        {
            if (this.serviceFabricDiscoveryTask == null)
            {
                this.logger.LogWarning("Service fabric discovery loop is not running.");
                return;
            }

            this.logger.LogInformation("Stopping service fabric discovery loop.");
            this.cts.Cancel();
            try
            {
                await this.serviceFabricDiscoveryTask;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                this.serviceFabricDiscoveryTask = null;
            }
        }

        private async Task ServiceFabricDiscoveryLoop(CancellationToken cancellation)
        {
            var errorReporter = new LoggerConfigErrorReporter(this.logger);
            while (true)
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    await this.serviceFabricDiscoveryWorker.ExecuteAsync(this.options, cancellation);
                    await this.gatewayManager.ApplyConfigurationsAsync(errorReporter, cancellation);
                    await this.timer.Delay(this.options.DiscoveryPeriod, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // Graceful shutdown
                    this.logger.LogInformation("Service Fabric discovery loop is ending gracefully");
                    return;
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    this.logger.LogError(ex, "Swallowing unhandled exception from service Fabric loop...");
                }
            }
        }
    }
}
