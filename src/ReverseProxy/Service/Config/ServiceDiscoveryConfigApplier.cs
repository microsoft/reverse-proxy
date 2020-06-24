// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Reacts to configuration changes for type <see cref="ServiceDiscoveryConfig"/>
    /// via <see cref="IOptionsMonitor{TOptions}"/>, and applies configurations
    /// to the Island Gateway core.
    /// When configs are loaded from appsettings.json, this takes care of hot updates
    /// when appsettings.json is modified on disk.
    /// </summary>
    // TODO: This is needs to be wired up with the Reverse Proxy core, and should probably be re-written from scratch.
    internal class ServiceDiscoveryConfigApplier : IHostedService, IDisposable
    {
        private readonly ILogger<ServiceDiscoveryConfigApplier> _logger;
        private readonly IEnumerable<IServiceDiscovery> _serviceDiscoveries;
        private readonly IDisposable _subscription;

        private IServiceDiscovery _currentServiceDiscovery;
        private bool _discoveryRunning;
        private bool _disposed;

        public ServiceDiscoveryConfigApplier(
            ILogger<ServiceDiscoveryConfigApplier> logger,
            IEnumerable<IServiceDiscovery> serviceDiscoveries,
            IOptionsMonitor<ServiceDiscoveryConfig> gatewayConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceDiscoveries = serviceDiscoveries ?? throw new ArgumentNullException(nameof(serviceDiscoveries));
            _ = gatewayConfig ?? throw new ArgumentNullException(nameof(gatewayConfig));

            _subscription = gatewayConfig.OnChange((newConfig, name) => Apply(newConfig));
            Apply(gatewayConfig.CurrentValue);
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

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_discoveryRunning)
                {
                    await _currentServiceDiscovery.StopAsync(cancellationToken);
                }
            }
            finally
            {
                _discoveryRunning = false;
                _currentServiceDiscovery = null;
            }
        }

        private async void Apply(ServiceDiscoveryConfig config)
        {
            if (config == null)
            {
                return;
            }

            _logger.LogInformation("Applying gateway configs");
            try
            {
                await StartServiceDiscoveryAsync(config);
                await SetServiceDiscoveryConfigAsync(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to apply configs.");
            }
        }

        private async Task StartServiceDiscoveryAsync(ServiceDiscoveryConfig config)
        {
            var newServiceDiscoveryName = config.ServiceDiscoveryName;
            if (_currentServiceDiscovery != null)
            {
                if (_currentServiceDiscovery.Name != newServiceDiscoveryName)
                {
                    await _currentServiceDiscovery.StopAsync(CancellationToken.None);
                    _logger.LogInformation($"Stopped {_currentServiceDiscovery.Name} service discovery.");
                    _discoveryRunning = false;
                }
                else
                {
                    // Nothing to change
                    return;
                }
            }
            if (string.IsNullOrEmpty(newServiceDiscoveryName))
            {
                throw new Exception("A service discovery name is needed.");
            }

            var newServiceDiscovery = _serviceDiscoveries.FirstOrDefault((serviceDiscovery) => serviceDiscovery.Name == config.ServiceDiscoveryName);
            if (newServiceDiscovery == null)
            {
                throw new Exception($"No registered service discovery '{newServiceDiscoveryName}' was found.");
            }
            newServiceDiscovery.Start();
            _logger.LogInformation($"Started '{newServiceDiscovery.Name}' service discovery.");
            _discoveryRunning = true;
            _currentServiceDiscovery = newServiceDiscovery;
        }

        private async Task SetServiceDiscoveryConfigAsync(ServiceDiscoveryConfig config)
        {
            if (_currentServiceDiscovery == null)
            {
                return;
            }

            if (!config.ServiceDiscoveryConfigs.TryGetValue(_currentServiceDiscovery.Name, out var serviceDiscoveryConfig))
            {
                throw new Exception($"Could not find '{_currentServiceDiscovery.Name}' in service discovery configs.");
            }

            await _currentServiceDiscovery.SetConfigAsync(serviceDiscoveryConfig, CancellationToken.None);
        }
    }
}
