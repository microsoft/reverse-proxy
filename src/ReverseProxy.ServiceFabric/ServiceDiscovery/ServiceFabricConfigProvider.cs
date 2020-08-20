// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// Periodically calls Service Fabric API's to discover services and their configurations.
    /// Use <see cref="ServiceFabricDiscoveryOptions"/> to configure Service Fabric service discovery.
    /// </summary>
    internal class ServiceFabricConfigProvider : IProxyConfigProvider, IAsyncDisposable
    {
        private readonly object _lockObject = new object();
        private readonly TaskCompletionSource<int> _initalConfigLoadTcs = new TaskCompletionSource<int>();
        private readonly ILogger<ServiceFabricConfigProvider> _logger;
        private readonly IMonotonicTimer _timer;
        private readonly IDiscoverer _discoverer;
        private readonly IOptionsMonitor<ServiceFabricDiscoveryOptions> _optionsMonitor;

        private volatile ConfigurationSnapshot _snapshot;
        private CancellationTokenSource _changeToken;
        private bool _disposed;

        private readonly CancellationTokenSource _backgroundCts;
        private readonly Task _backgroundTask;

        public ServiceFabricConfigProvider(
            ILogger<ServiceFabricConfigProvider> logger,
            IMonotonicTimer timer,
            IDiscoverer discoverer,
            IOptionsMonitor<ServiceFabricDiscoveryOptions> optionsMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));

            _backgroundCts = new CancellationTokenSource();
            _backgroundTask = ServiceFabricDiscoveryLoop();
        }

        public IProxyConfig GetConfig()
        {
            if (_snapshot != null)
            {
                return _snapshot;
            }

            WaitForDiscoveryOrCreateEmptyConfig();
            Debug.Assert(_snapshot != null);
            return _snapshot;

            void WaitForDiscoveryOrCreateEmptyConfig()
            {
                if (_optionsMonitor.CurrentValue.AllowStartBeforeDiscovery)
                {
                    lock (_lockObject)
                    {
                        if (_snapshot == null)
                        {
                            _logger.LogInformation($"Proceeding without initial Service Fabric discovery results due to ${nameof(_optionsMonitor.CurrentValue.AllowStartBeforeDiscovery)} = true.");
                            UpdateSnapshot(new List<ProxyRoute>(), new List<Cluster>());
                        }
                    }
                }
                else
                {
                    // NOTE: The callstack up to this point is already synchronously blocking.
                    // There isn't much we can do to avoid this blocking wait on startup.
                    _logger.LogInformation($"Waiting for initial Service Fabric discovery results due to ${nameof(_optionsMonitor.CurrentValue.AllowStartBeforeDiscovery)} = false.");
                    _initalConfigLoadTcs.Task.Wait();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                _changeToken?.Dispose();

                // Stop discovery loop...
                _backgroundCts.Cancel();
                await _backgroundTask;
                _backgroundCts.Dispose();
            }
        }

        private void UpdateSnapshot(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
        {
            // Prevent overlapping updates
            lock (_lockObject)
            {
                Log.LoadData(_logger);
                using var oldToken = _changeToken;
                _changeToken = new CancellationTokenSource();
                _snapshot = new ConfigurationSnapshot()
                {
                    Routes = routes,
                    Clusters = clusters,
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

            _initalConfigLoadTcs.TrySetResult(0);
        }

        private async Task ServiceFabricDiscoveryLoop()
        {
            _logger.LogInformation("Service Fabric discovery loop is starting");
            var first = true;
            while (true)
            {
                try
                {
                    _backgroundCts.Token.ThrowIfCancellationRequested();
                    if (!first)
                    {
                        await _timer.Delay(_optionsMonitor.CurrentValue.DiscoveryPeriod, _backgroundCts.Token);
                    }

                    var result = await _discoverer.DiscoverAsync(_backgroundCts.Token);
                    UpdateSnapshot(result.Routes, result.Clusters);
                }
                catch (OperationCanceledException) when (_backgroundCts.Token.IsCancellationRequested)
                {
                    // Graceful shutdown
                    _logger.LogInformation("Service Fabric discovery loop is ending gracefully");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Swallowing unhandled exception from Service Fabric loop...");
                }

                first = false;
            }
        }

        // TODO: Perhaps YARP should provide this type?
        private class ConfigurationSnapshot : IProxyConfig
        {
            public IReadOnlyList<ProxyRoute> Routes { get; internal set; }

            public IReadOnlyList<Cluster> Clusters { get; internal set; }

            public IChangeToken ChangeToken { get; internal set; }
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
