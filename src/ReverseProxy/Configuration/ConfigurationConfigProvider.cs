// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Configuration.Contract;
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
        private readonly ICertificateConfigLoader _certificateConfigLoader;
        private ConfigurationSnapshot _snapshot;
        private CancellationTokenSource _changeToken;
        private bool _disposed;
        private IDisposable _subscription;

        public ConfigurationConfigProvider(
            ILogger<ConfigurationConfigProvider> logger,
            IOptionsMonitor<ConfigurationOptions> optionsMonitor,
            ICertificateConfigLoader certificateConfigLoader)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _certificateConfigLoader = certificateConfigLoader ?? throw new ArgumentNullException(nameof(certificateConfigLoader));
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
                    Routes = options.Routes.Select(r => Convert(r)).ToList().AsReadOnly(),
                    Clusters = options.Clusters.Values.Select(r => Convert(r)).ToList().AsReadOnly(),
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

        private Abstractions.Cluster Convert(Cluster options)
        {
            var cluster = new Abstractions.Cluster
            {
                Id = options.Id,
                CircuitBreakerOptions = Convert(options.CircuitBreakerOptions),
                QuotaOptions = Convert(options.QuotaOptions),
                PartitioningOptions = Convert(options.PartitioningOptions),
                LoadBalancing = Convert(options.LoadBalancing),
                SessionAffinity = Convert(options.SessionAffinity),
                HealthCheckOptions = Convert(options.HealthCheckOptions),
                HttpClientOptions = Convert(options.Id, options.HttpClientOptions),
                Metadata = options.Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase)
            };
            foreach(var destination in options.Destinations)
            {
                cluster.Destinations.Add(destination.Key, Convert(destination.Value));
            }
            return cluster;
        }

        private Abstractions.ProxyRoute Convert(ProxyRoute options)
        {
            var route = new Abstractions.ProxyRoute
            {
                RouteId = options.RouteId,
                Order = options.Order,
                ClusterId = options.ClusterId,
                AuthorizationPolicy = options.AuthorizationPolicy,
                CorsPolicy = options.CorsPolicy,
                Metadata = options.Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase),
                Transforms = options.Transforms?.Select(d => new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase)).ToList<IDictionary<string, string>>(),
            };
            Convert(route.Match, options.Match);
            return route;
        }

        private void Convert(Abstractions.ProxyMatch proxyMatch, ProxyMatch options)
        {
            if (options == null)
            {
                return;
            }

            proxyMatch.Methods = options.Methods?.ToArray();
            proxyMatch.Hosts = options.Hosts?.ToArray();
            proxyMatch.Path = options.Path;
        }

        private Abstractions.CircuitBreakerOptions Convert(CircuitBreakerOptions options)
        {
            if(options == null)
            {
                return null;
            }

            return new Abstractions.CircuitBreakerOptions
            {
                MaxConcurrentRequests = options.MaxConcurrentRequests,
                MaxConcurrentRetries = options.MaxConcurrentRetries,
            };
        }

        private Abstractions.QuotaOptions Convert(QuotaOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new Abstractions.QuotaOptions
            {
                Average = options.Average,
                Burst = options.Burst,
            };
        }

        private Abstractions.ClusterPartitioningOptions Convert(ClusterPartitioningOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new Abstractions.ClusterPartitioningOptions
            {
                PartitionCount = options.PartitionCount,
                PartitionKeyExtractor = options.PartitionKeyExtractor,
                PartitioningAlgorithm = options.PartitioningAlgorithm,
            };
        }

        private Abstractions.LoadBalancingOptions Convert(LoadBalancingOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new Abstractions.LoadBalancingOptions
            {
                Mode = (Abstractions.LoadBalancingMode)(int)options.Mode,
            };
        }

        private Abstractions.SessionAffinityOptions Convert(SessionAffinityOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new Abstractions.SessionAffinityOptions
            {
                Enabled = options.Enabled,
                Mode = options.Mode,
                FailurePolicy = options.FailurePolicy,
                Settings = options.Settings?.DeepClone(StringComparer.OrdinalIgnoreCase)
            };
        }

        private Abstractions.HealthCheckOptions Convert(HealthCheckOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new Abstractions.HealthCheckOptions
            {
                Enabled = options.Enabled,
                Interval = options.Interval,
                Timeout = options.Timeout,
                Port = options.Port,
                Path = options.Path,
            };
        }

        private Abstractions.ProxyHttpClientOptions Convert(string clusterId, ProxyHttpClientOptions options)
        {
            if (options == null)
            {
                return null;
            }

            var clientCertificate = options.ClientCertificate != null ? _certificateConfigLoader.LoadCertificate(clusterId, options.ClientCertificate) : null;
            return new Abstractions.ProxyHttpClientOptions
            {
                SslApplicationProtocols = options.SslApplicationProtocols.CloneList(),
                RevocationCheckMode = options.RevocationCheckMode,
                CipherSuitesPolicy = options.CipherSuitesPolicy.CloneList(),
                SslProtocols = options.SslProtocols.CloneList(),
                EncryptionPolicy = options.EncryptionPolicy,
                ValidateRemoteCertificate = options.ValidateRemoteCertificate,
                ClientCertificate = clientCertificate,
                MaxConnectionsPerServer = options.MaxConnectionsPerServer
            };
        }

        private Abstractions.Destination Convert(Destination options)
        {
            if (options == null)
            {
                return null;
            }

            return new Abstractions.Destination
            {
                Address = options.Address,
                ProtocolVersion = options.ProtocolVersion,
                Metadata = options.Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase),
            };
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
