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
        private readonly ILogger<GatewayConfigApplier> _logger;
        private readonly IBackendsRepo _backendsRepo;
        private readonly IBackendEndpointsRepo _endpointsRepo;
        private readonly IRoutesRepo _routesRepo;
        private readonly IIslandGatewayConfigManager _gatewayManager;

        private bool _disposed;
        private IDisposable _subscription;

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

            this._logger = logger;
            this._backendsRepo = backendsRepo;
            this._endpointsRepo = endpointsRepo;
            this._routesRepo = routesRepo;
            this._gatewayManager = gatewayManager;

            this._subscription = gatewayConfig.OnChange((newConfig, name) => this.Apply(newConfig));
            this.Apply(gatewayConfig.CurrentValue);
        }

        public void Dispose()
        {
            if (!this._disposed)
            {
                this._subscription.Dispose();
                this._disposed = true;
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

            this._logger.LogInformation("Applying gateway configs");
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
                this._logger.LogError(ex, $"Failed to apply new configs: {ex.Message}");
            }
        }

        private async Task ApplyStaticConfigsAsync(StaticDiscoveryOptions options, CancellationToken cancellation)
        {
            if (options == null)
            {
                return;
            }

            await this._backendsRepo.SetBackendsAsync(options.Backends, cancellation);
            foreach (var kvp in options.Endpoints)
            {
                await this._endpointsRepo.SetEndpointsAsync(kvp.Key, kvp.Value, cancellation);
            }

            await this._routesRepo.SetRoutesAsync(options.Routes, cancellation);

            var errorReporter = new LoggerConfigErrorReporter(this._logger);
            await this._gatewayManager.ApplyConfigurationsAsync(errorReporter, cancellation);
        }

        private class LoggerConfigErrorReporter : IConfigErrorReporter
        {
            private readonly ILogger _logger;

            public LoggerConfigErrorReporter(ILogger logger)
            {
                this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public void ReportError(string code, string itemId, string message)
            {
                this._logger.LogWarning($"Config error: '{code}', '{itemId}', '{message}'.");
            }
        }
    }
}
