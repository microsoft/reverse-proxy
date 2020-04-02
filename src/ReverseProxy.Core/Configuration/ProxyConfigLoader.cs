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

namespace Microsoft.ReverseProxy.Core.Configuration
{
    // TODO: It's weird that this is an IHostedService.
    // Could this be moved to MapReverseProxy?

    /// <summary>
    /// Reacts to configuration changes for type <see cref="ProxyConfigRoot"/>
    /// via <see cref="IOptionsMonitor{TOptions}"/>, and applies configurations
    /// to the Reverse Proxy core.
    /// When configs are loaded from appsettings.json, this takes care of hot updates
    /// when appsettings.json is modified on disk.
    /// </summary>
    internal class ProxyConfigLoader : IHostedService, IDisposable
    {
        private readonly IOptions<ProxyConfigLoaderOptions> _options;
        private readonly ILogger<ProxyConfigLoader> _logger;
        private readonly IBackendsRepo _backendsRepo;
        private readonly IBackendEndpointsRepo _endpointsRepo;
        private readonly IRoutesRepo _routesRepo;
        private readonly IReverseProxyConfigManager _proxyManager;
        private readonly IOptionsMonitor<ProxyConfigOptions> _proxyConfig;
        private bool _disposed;
        private IDisposable _subscription;

        public ProxyConfigLoader(
            IOptions<ProxyConfigLoaderOptions> options,
            ILogger<ProxyConfigLoader> logger,
            IBackendsRepo backendsRepo,
            IBackendEndpointsRepo endpointsRepo,
            IRoutesRepo routesRepo,
            IReverseProxyConfigManager proxyManager,
            IOptionsMonitor<ProxyConfigOptions> proxyConfig)
        {
            Contracts.CheckValue(options, nameof(options));
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(backendsRepo, nameof(backendsRepo));
            Contracts.CheckValue(endpointsRepo, nameof(endpointsRepo));
            Contracts.CheckValue(routesRepo, nameof(routesRepo));
            Contracts.CheckValue(proxyManager, nameof(proxyManager));
            Contracts.CheckValue(proxyConfig, nameof(proxyConfig));

            _options = options;
            _logger = logger;
            _backendsRepo = backendsRepo;
            _endpointsRepo = endpointsRepo;
            _routesRepo = routesRepo;
            _proxyManager = proxyManager;
            _proxyConfig = proxyConfig;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _subscription?.Dispose();
                _disposed = true;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_options.Value.ReloadOnChange)
            {
                _subscription = _proxyConfig.OnChange((newConfig, name) => _ = ApplyAsync(newConfig));
            }
            return ApplyAsync(_proxyConfig.CurrentValue);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscription?.Dispose();
            return Task.CompletedTask;
        }

        private async Task ApplyAsync(ProxyConfigOptions config)
        {
            if (config == null)
            {
                return;
            }

            _logger.LogInformation("Applying proxy configs");
            try
            {
                await _backendsRepo.SetBackendsAsync(config.Backends, CancellationToken.None);
                foreach (var kvp in config.Endpoints)
                {
                    await _endpointsRepo.SetEndpointsAsync(kvp.Key, kvp.Value, CancellationToken.None);
                }

                await _routesRepo.SetRoutesAsync(config.Routes, CancellationToken.None);

                var errorReporter = new LoggerConfigErrorReporter(_logger);
                await _proxyManager.ApplyConfigurationsAsync(errorReporter, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to apply new configs: {ex.Message}");
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
                _logger.LogWarning($"Config error: '{code}', '{itemId}', '{message}'.");
            }
        }
    }
}
