// <copyright file="GatewayConfigApplier.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Abstractions;
using IslandGateway.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IslandGateway.Sample.Config
{
    /// <summary>
    /// Reacts to configuration changes for type <see cref="GatewayConfigRoot"/>
    /// via <see cref="IOptionsMonitor{TOptions}"/>, and applies configurations
    /// to the Island Gateway core.
    /// When configs are loaded from appsettings.json, this takes care of hot updates
    /// when appsettings.json is modified on disk.
    /// </summary>
    internal class GatewayConfigApplier : IHostedService, IDisposable
    {
        private readonly ILogger<GatewayConfigApplier> logger;
        private readonly IBackendsRepo backendsRepo;
        private readonly IBackendEndpointsRepo endpointsRepo;
        private readonly IRoutesRepo routesRepo;
        private readonly IIslandGatewayConfigManager gatewayManager;

        private bool disposed;
        private IDisposable subscription;

        public GatewayConfigApplier(
            ILogger<GatewayConfigApplier> logger,
            IBackendsRepo backendsRepo,
            IBackendEndpointsRepo endpointsRepo,
            IRoutesRepo routesRepo,
            IIslandGatewayConfigManager gatewayManager,
            IOptionsMonitor<GatewayConfigRoot> gatewayConfig)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(backendsRepo, nameof(backendsRepo));
            Contracts.CheckValue(endpointsRepo, nameof(endpointsRepo));
            Contracts.CheckValue(routesRepo, nameof(routesRepo));
            Contracts.CheckValue(gatewayManager, nameof(gatewayManager));
            Contracts.CheckValue(gatewayConfig, nameof(gatewayConfig));

            this.logger = logger;
            this.backendsRepo = backendsRepo;
            this.endpointsRepo = endpointsRepo;
            this.routesRepo = routesRepo;
            this.gatewayManager = gatewayManager;

            this.subscription = gatewayConfig.OnChange((newConfig, name) => this.Apply(newConfig));
            this.Apply(gatewayConfig.CurrentValue);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.subscription.Dispose();
                this.disposed = true;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Nothing to start
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop
            return Task.CompletedTask;
        }

        private async void Apply(GatewayConfigRoot config)
        {
            if (config == null)
            {
                return;
            }

            this.logger.LogInformation("Applying gateway configs");
            try
            {
                switch (config.DiscoveryMechanism)
                {
                    case "static":
                        await this.ApplyStaticConfigsAsync(config.StaticDiscoveryOptions, CancellationToken.None);
                        break;
                    default:
                        throw new Exception($"Config discovery mechanism '{config.DiscoveryMechanism}' is not supported.");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Failed to apply new configs: {ex.Message}");
            }
        }

        private async Task ApplyStaticConfigsAsync(StaticDiscoveryOptions options, CancellationToken cancellation)
        {
            if (options == null)
            {
                return;
            }

            await this.backendsRepo.SetBackendsAsync(options.Backends, cancellation);
            foreach (var kvp in options.Endpoints)
            {
                await this.endpointsRepo.SetEndpointsAsync(kvp.Key, kvp.Value, cancellation);
            }

            await this.routesRepo.SetRoutesAsync(options.Routes, cancellation);

            var errorReporter = new LoggerConfigErrorReporter(this.logger);
            await this.gatewayManager.ApplyConfigurationsAsync(errorReporter, cancellation);
        }

        private class LoggerConfigErrorReporter : IConfigErrorReporter
        {
            private readonly ILogger logger;

            public LoggerConfigErrorReporter(ILogger logger)
            {
                this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public void ReportError(string code, string itemId, string message)
            {
                this.logger.LogWarning($"Config error: '{code}', '{itemId}', '{message}'.");
            }
        }
    }
}
