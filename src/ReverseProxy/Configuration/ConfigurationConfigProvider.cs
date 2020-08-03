// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.Configuration
{
    /// <summary>
    /// Reacts to configuration changes for type <see cref="ConfigurationOptions"/>
    /// via <see cref="IOptionsMonitor{TOptions}"/>, and applies configurations
    /// to the Reverse Proxy core.
    /// When configs are loaded from appsettings.json, this takes care of hot updates
    /// when appsettings.json is modified on disk.
    /// </summary>
    internal class ConfigurationConfigProvider : IProxyConfigProvider, IDisposable
    {
        private readonly ILogger<ConfigurationConfigProvider> _logger;
        private readonly IOptionsMonitor<ConfigurationOptions> _optionsMonitor;
        private ConfigurationSnapshot _snapshot;
        private CancellationTokenSource _changeToken;
        private bool _disposed;
        private IDisposable _subscription;

        public ConfigurationConfigProvider(
            ILogger<ConfigurationConfigProvider> logger,
            IOptionsMonitor<ConfigurationOptions> optionsMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _subscription?.Dispose();
                _changeToken?.Dispose();
                _disposed = true;
            }
        }

        public IProxyConfig GetConfig()
        {
            // First time load
            if (_snapshot == null)
            {
                CreateSnapshot(_optionsMonitor.CurrentValue);
                _subscription = _optionsMonitor.OnChange((newConfig) => CreateSnapshot(newConfig));
            }
            return _snapshot;
        }

        private void CreateSnapshot(ConfigurationOptions options)
        {
            Log.ApplyProxyConfig(_logger);
            var oldToken = _changeToken;
            _changeToken = new CancellationTokenSource();
            _snapshot = new ConfigurationSnapshot()
            {
                Routes = options.Routes,
                Clusters = options.Clusters.Values.ToList(),
                ChangeToken = new CancellationChangeToken(_changeToken.Token)
            };

            try
            {
                oldToken?.Cancel();
            }
            catch (Exception ex)
            {
                Log.ApplyProxyConfigFailed(_logger, ex.Message, ex);
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _applyProxyConfigFailed = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.ApplyProxyConfigFailed,
                "Failed to apply new configs: {errorMessage}");

            private static readonly Action<ILogger, Exception> _applyProxyConfig = LoggerMessage.Define(
                LogLevel.Information,
                EventIds.ApplyProxyConfig,
                "Applying proxy configs");

            public static void ApplyProxyConfigFailed(ILogger logger, string errorMessage, Exception exception)
            {
                _applyProxyConfigFailed(logger, errorMessage, exception);
            }

            public static void ApplyProxyConfig(ILogger logger)
            {
                _applyProxyConfig(logger, null);
            }
        }
    }
}
