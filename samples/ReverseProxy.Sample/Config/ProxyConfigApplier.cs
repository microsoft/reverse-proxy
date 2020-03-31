// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Sample.Config
{
    /// <summary>
    /// Reacts to configuration changes for type <see cref="ProxyConfigRoot"/>
    /// via <see cref="IOptionsMonitor{TOptions}"/>, and applies configurations
    /// to the Reverse Proxy core.
    /// When configs are loaded from appsettings.json, this takes care of hot updates
    /// when appsettings.json is modified on disk.
    /// </summary>
    internal class ProxyConfigApplier : IHostedService, IDisposable
    {
        private readonly ILogger<ProxyConfigApplier> _logger;
        private readonly IBackendsRepo _backendsRepo;
        private readonly IBackendEndpointsRepo _endpointsRepo;
        private readonly IRoutesRepo _routesRepo;
        private readonly IReverseProxyConfigManager _proxyManager;

        private bool _disposed;
        private readonly IDisposable _subscription;

        public ProxyConfigApplier(
            ILogger<ProxyConfigApplier> logger,
            IBackendsRepo backendsRepo,
            IBackendEndpointsRepo endpointsRepo,
            IRoutesRepo routesRepo,
            IReverseProxyConfigManager proxyManager,
            IOptionsMonitor<ProxyConfigRoot> proxyConfig)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(backendsRepo, nameof(backendsRepo));
            Contracts.CheckValue(endpointsRepo, nameof(endpointsRepo));
            Contracts.CheckValue(routesRepo, nameof(routesRepo));
            Contracts.CheckValue(proxyManager, nameof(proxyManager));
            Contracts.CheckValue(proxyConfig, nameof(proxyConfig));

            _logger = logger;
            _backendsRepo = backendsRepo;
            _endpointsRepo = endpointsRepo;
            _routesRepo = routesRepo;
            _proxyManager = proxyManager;

            _subscription = proxyConfig.OnChange((newConfig, name) => Apply(newConfig));
            Apply(proxyConfig.CurrentValue);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _subscription.Dispose();
                _disposed = true;
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

        private async void Apply(ProxyConfigRoot config)
        {
            if (config == null)
            {
                return;
            }

            _logger.LogInformation("Applying proxy configs");
            try
            {
                switch (config.DiscoveryMechanism)
                {
                    case "static":
                        await ApplyStaticConfigsAsync(config.StaticDiscoveryOptions, CancellationToken.None);
                        break;
                    default:
                        throw new Exception($"Config discovery mechanism '{config.DiscoveryMechanism}' is not supported.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to apply new configs: {ex.Message}");
            }
        }

        private async Task ApplyStaticConfigsAsync(StaticDiscoveryOptions options, CancellationToken cancellation)
        {
            if (options == null)
            {
                return;
            }

            await _backendsRepo.SetBackendsAsync(options.Backends, cancellation);
            foreach (var kvp in options.Endpoints)
            {
                await _endpointsRepo.SetEndpointsAsync(kvp.Key, kvp.Value, cancellation);
            }

            await _routesRepo.SetRoutesAsync(options.Routes, cancellation);

            var errorReporter = new LoggerConfigErrorReporter(_logger);
            await _proxyManager.ApplyConfigurationsAsync(errorReporter, cancellation);
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
                _logger.LogWarning($"Config error: '{code}', '{itemId}', '{message}'.");
            }
        }
    }
}
