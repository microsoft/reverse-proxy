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
        private readonly object _lockObject = new object();
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
                _subscription = _optionsMonitor.OnChange(UpdateSnapshot);
                UpdateSnapshot(_optionsMonitor.CurrentValue);
            }
            return _snapshot;
        }

        private void UpdateSnapshot(ConfigurationOptions options)
        {
            // Prevent overlapping updates, especially on startup.
            lock (_lockObject)
            {
                Log.LoadData(_logger);
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
                    oldToken?.Cancel(throwOnFirstException: false);
                }
                catch (Exception ex)
                {
                    Log.ErrorSignalingChange(_logger, ex);
                }
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _errorSignalingChange = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.ErrorSignalingChange,
                "An exception was thrown from the change notification.");

            private static readonly Action<ILogger, Exception> _loadData = LoggerMessage.Define(
                LogLevel.Information,
                EventIds.LoadData,
                "Loading proxy data from config.");

            public static void ErrorSignalingChange(ILogger logger, Exception exception)
            {
                _errorSignalingChange(logger, exception);
            }

            public static void LoadData(ILogger logger)
            {
                _loadData(logger, null);
            }
        }
    }
}
