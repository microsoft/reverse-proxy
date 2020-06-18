// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Configuration
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
        private readonly ILogger<ProxyConfigLoader> _logger;
        private readonly IClustersRepo _clustersRepo;
        private readonly IRoutesRepo _routesRepo;
        private readonly IReverseProxyConfigManager _proxyManager;
        private readonly IOptionsMonitor<ProxyConfigOptions> _proxyConfig;
        private bool _disposed;
        private IDisposable _subscription;

        public ProxyConfigLoader(
            ILogger<ProxyConfigLoader> logger,
            IClustersRepo clustersRepo,
            IRoutesRepo routesRepo,
            IReverseProxyConfigManager proxyManager,
            IOptionsMonitor<ProxyConfigOptions> proxyConfig)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(clustersRepo, nameof(clustersRepo));
            Contracts.CheckValue(routesRepo, nameof(routesRepo));
            Contracts.CheckValue(proxyManager, nameof(proxyManager));
            Contracts.CheckValue(proxyConfig, nameof(proxyConfig));

            _logger = logger;
            _clustersRepo = clustersRepo;
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
            _subscription = _proxyConfig.OnChange((newConfig, name) => _ = ApplyAsync(newConfig));
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

            Log.ApplyProxyConfig(_logger);
            try
            {
                await _clustersRepo.SetClustersAsync(config.Clusters, CancellationToken.None);
                await _routesRepo.SetRoutesAsync(config.Routes, CancellationToken.None);

                var errorReporter = new LoggerConfigErrorReporter(_logger);
                await _proxyManager.ApplyConfigurationsAsync(errorReporter, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.ApplyProxyConfigFailed(_logger, ex.Message, ex);
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
                Log.ConfigError(_logger, code, itemId, message);
            }

            public void ReportError(string code, string itemId, string message, Exception ex)
            {
                Log.ConfigError(_logger, ex, code, itemId, message);
            }
        }


        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _applyProxyConfigFailed = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.ApplyProxyConfigFailed,
                "Failed to apply new configs: {errorMessage}");

            private static readonly Action<ILogger, string, string, string, Exception> _configError = LoggerMessage.Define<string, string, string>(
                LogLevel.Warning,
                EventIds.ConfigError,
                "Config error: '{code}', '{itemId}', '{message}'.");

            private static readonly Action<ILogger, Exception> _applyProxyConfig = LoggerMessage.Define(
                LogLevel.Information,
                EventIds.ApplyProxyConfig,
                "Applying proxy configs");

            public static void ApplyProxyConfigFailed(ILogger logger, string errorMessage, Exception exception)
            {
                _applyProxyConfigFailed(logger, errorMessage, exception);
            }

            public static void ConfigError(ILogger logger, string code, string itemId, string message)
            {
                _configError(logger, code, itemId, message, null);
            }

            public static void ConfigError(ILogger logger, Exception ex, string code, string itemId, string message)
            {
                _configError(logger, code, itemId, message, ex);
            }

            public static void ApplyProxyConfig(ILogger logger)
            {
                _applyProxyConfig(logger, null);
            }
        }
    }
}
