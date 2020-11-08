// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.Configuration
{
    /// <summary>
    /// Reacts to configuration changes and applies configurations to the Reverse Proxy core.
    /// When configs are loaded from appsettings.json, this takes care of hot updates
    /// when appsettings.json is modified on disk.
    /// </summary>
    internal class ConfigurationConfigProvider : IProxyConfigProvider, IDisposable
    {
        private readonly object _lockObject = new object();
        private readonly ILogger<ConfigurationConfigProvider> _logger;
        private readonly IConfiguration _configuration;
        private readonly ICertificateConfigLoader _certificateConfigLoader;
        private ConfigurationSnapshot _snapshot;
        private CancellationTokenSource _changeToken;
        private bool _disposed;
        private IDisposable _subscription;

        public ConfigurationConfigProvider(
            ILogger<ConfigurationConfigProvider> logger,
            IConfiguration configuration,
            ICertificateConfigLoader certificateConfigLoader)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _certificateConfigLoader = certificateConfigLoader ?? throw new ArgumentNullException(nameof(certificateConfigLoader));
        }

        // Used by tests
        internal LinkedList<WeakReference<X509Certificate2>> Certificates { get; } = new LinkedList<WeakReference<X509Certificate2>>();

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var certificateRef in Certificates)
                {
                    if (certificateRef.TryGetTarget(out var certificate))
                    {
                        certificate.Dispose();
                    }
                }

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
                _subscription = ChangeToken.OnChange(_configuration.GetReloadToken, UpdateSnapshot);
                UpdateSnapshot();
            }
            return _snapshot;
        }

        private void UpdateSnapshot()
        {
            // Prevent overlapping updates, especially on startup.
            lock (_lockObject)
            {
                Log.LoadData(_logger);
                ConfigurationSnapshot newSnapshot = null;
                try
                {
                    newSnapshot = new ConfigurationSnapshot();

                    foreach (var section in _configuration.GetSection("Clusters").GetChildren())
                    {
                        newSnapshot.Clusters.Add(CreateCluster(section));
                    }

                    foreach (var section in _configuration.GetSection("Routes").GetChildren())
                    {
                        newSnapshot.Routes.Add(CreateRoute(section));
                    }

                    PurgeCertificateList();
                }
                catch (Exception ex)
                {
                    Log.ConfigurationDataConversionFailed(_logger, ex);

                    // Re-throw on the first time load to prevent app from starting.
                    if (_snapshot == null)
                    {
                        throw;
                    }

                    return;
                }

                var oldToken = _changeToken;
                _changeToken = new CancellationTokenSource();
                newSnapshot.ChangeToken = new CancellationChangeToken(_changeToken.Token);
                _snapshot = newSnapshot;

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

        private void PurgeCertificateList()
        {
            var next = Certificates.First;
            while (next != null)
            {
                var current = next;
                next = next.Next;
                // Remove a certificate from the collection if either it has been already collected or at least disposed.
                if (!current.Value.TryGetTarget(out var cert) || cert.Handle == default)
                {
                    Certificates.Remove(current);
                }
            }
        }

        private Cluster CreateCluster(IConfigurationSection section)
        {
            var cluster = new Cluster
            {
                Id = section.Key,
                CircuitBreaker = CreateCircuitBreakerOptions(section.GetSection(nameof(Cluster.CircuitBreaker))),
                Quota = CreateQuotaOptions(section.GetSection(nameof(Cluster.Quota))),
                Partitioning = CreateClusterPartitioningOptions(section.GetSection(nameof(Cluster.Partitioning))),
                LoadBalancing = CreateLoadBalancingOptions(section.GetSection(nameof(Cluster.LoadBalancing))),
                SessionAffinity = CreateSessionAffinityOptions(section.GetSection(nameof(Cluster.SessionAffinity))),
                HealthCheck = CreateHealthCheckOptions(section.GetSection(nameof(Cluster.HealthCheck))),
                HttpClient = CreateProxyHttpClientOptions(section.GetSection(nameof(Cluster.HttpClient))),
                HttpRequest = CreateProxyRequestOptions(section.GetSection(nameof(Cluster.HttpRequest))),
                Metadata = CreateStringDictionary(section.GetSection(nameof(Cluster.Metadata)))
            };

            foreach (var destination in section.GetSection(nameof(Cluster.Destinations)).GetChildren())
            {
                cluster.Destinations.Add(destination.Key, CreateDestination(destination));
            }

            return cluster;
        }

        private static ProxyRoute CreateRoute(IConfigurationSection section)
        {
            var route = new ProxyRoute
            {
                RouteId = section[nameof(ProxyRoute.RouteId)],
                Order = int.TryParse(section[nameof(ProxyRoute.Order)], out var order) ? order : null,
                ClusterId = section[nameof(ProxyRoute.ClusterId)],
                AuthorizationPolicy = section[nameof(ProxyRoute.AuthorizationPolicy)],
                CorsPolicy = section[nameof(ProxyRoute.CorsPolicy)],
                Metadata = CreateStringDictionary(section.GetSection(nameof(ProxyRoute.Metadata))),
                Transforms = CreateTransforms(section.GetSection(nameof(ProxyRoute.Transforms))),
            };
            InitializeProxyMatch(route.Match, section.GetSection(nameof(ProxyRoute.Match)));
            return route;
        }

        private static IList<IDictionary<string, string>> CreateTransforms(IConfigurationSection section)
        {
            if (section?.GetChildren() is var children && children?.Any() is false)
            {
                return null;
            }

            return children.Select(s => s.GetChildren().ToDictionary(d => d.Key, d => d.Value, StringComparer.OrdinalIgnoreCase)).ToList<IDictionary<string, string>>();
        }

        private static void InitializeProxyMatch(ProxyMatch proxyMatch, IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return;
            }

            proxyMatch.Methods = GetStringArray(section.GetSection(nameof(ProxyMatch.Methods)));
            proxyMatch.Hosts = GetStringArray(section.GetSection(nameof(ProxyMatch.Hosts)));
            proxyMatch.Path = section[nameof(ProxyMatch.Path)];
            proxyMatch.Headers = CreateRouteHeaders(section.GetSection(nameof(ProxyMatch.Headers)));
        }

        private static IReadOnlyList<RouteHeader> CreateRouteHeaders(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return section.GetChildren().Select(data => CreateRouteHeader(data)).ToArray();
        }

        private static RouteHeader CreateRouteHeader(IConfigurationSection section)
        {
            var routeHeader = new RouteHeader()
            {
                Name = section[nameof(RouteHeader.Name)],
                Values = GetStringArray(section.GetSection(nameof(RouteHeader.Values))),
                Mode = Enum.TryParse<HeaderMatchMode>(section[nameof(RouteHeader.Mode)], out var mode) ? mode : default,
                IsCaseSensitive = bool.TryParse(section[nameof(RouteHeader.IsCaseSensitive)], out var isCaseSensitive) ? isCaseSensitive : default,
            };

            return routeHeader;
        }

        private static CircuitBreakerOptions CreateCircuitBreakerOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new CircuitBreakerOptions
            {
                MaxConcurrentRequests = int.TryParse(section[nameof(CircuitBreakerOptions.MaxConcurrentRequests)], out var maxConcurrentRequests) ? maxConcurrentRequests : default,
                MaxConcurrentRetries = int.TryParse(section[nameof(CircuitBreakerOptions.MaxConcurrentRetries)], out var maxConcurrentRetries) ? maxConcurrentRetries : default,
            };
        }

        private static QuotaOptions CreateQuotaOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new QuotaOptions
            {
                Average = double.TryParse(section[nameof(QuotaOptions.Average)], out var avg) ? avg : default,
                Burst = double.TryParse(section[nameof(QuotaOptions.Burst)], out var burst) ? burst : default,
            };
        }

        private static ClusterPartitioningOptions CreateClusterPartitioningOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new ClusterPartitioningOptions
            {
                PartitionCount = int.TryParse(section[nameof(ClusterPartitioningOptions.PartitionCount)], out var partitionCount) ? partitionCount : default,
                PartitionKeyExtractor = section[nameof(ClusterPartitioningOptions.PartitionKeyExtractor)],
                PartitioningAlgorithm = section[nameof(ClusterPartitioningOptions.PartitioningAlgorithm)],
            };
        }

        private static LoadBalancingOptions CreateLoadBalancingOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new LoadBalancingOptions
            {
                Mode = Enum.TryParse<LoadBalancingMode>(section[nameof(LoadBalancingOptions.Mode)], out var mode) ? mode : default,
            };
        }

        private static SessionAffinityOptions CreateSessionAffinityOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new SessionAffinityOptions
            {
                Enabled = bool.TryParse(section[nameof(SessionAffinityOptions.Enabled)], out var enabled) ? enabled : default,
                Mode = section[nameof(SessionAffinityOptions.Mode)],
                FailurePolicy = section[nameof(SessionAffinityOptions.FailurePolicy)],
                Settings = CreateStringDictionary(section.GetSection(nameof(SessionAffinityOptions.Settings)))
            };
        }

        private static HealthCheckOptions CreateHealthCheckOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new HealthCheckOptions
            {
                Passive = CreatePassiveHealthCheckOptions(section.GetSection(nameof(HealthCheckOptions.Passive))),
                Active = CreateActiveHealthCheckOptions(section.GetSection(nameof(HealthCheckOptions.Active)))
            };
        }

        private static PassiveHealthCheckOptions CreatePassiveHealthCheckOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new PassiveHealthCheckOptions
            {
                Enabled = bool.TryParse(section[nameof(PassiveHealthCheckOptions.Enabled)], out var enabled) ? enabled : default,
                Policy = section[nameof(PassiveHealthCheckOptions.Policy)],
                ReactivationPeriod = TimeSpan.TryParse(section[nameof(PassiveHealthCheckOptions.ReactivationPeriod)], out var period) ? period : default
            };
        }

        private static ActiveHealthCheckOptions CreateActiveHealthCheckOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new ActiveHealthCheckOptions
            {
                Enabled = bool.TryParse(section[nameof(ActiveHealthCheckOptions.Enabled)], out var enabled) ? enabled : default,
                Interval = TimeSpan.TryParse(section[nameof(ActiveHealthCheckOptions.Interval)], out var interval) ? interval : default,
                Timeout = TimeSpan.TryParse(section[nameof(ActiveHealthCheckOptions.Timeout)], out var timeout) ? timeout : default,
                Policy = section[nameof(ActiveHealthCheckOptions.Policy)],
                Path = section[nameof(ActiveHealthCheckOptions.Path)]
            };
        }

        private ProxyHttpClientOptions CreateProxyHttpClientOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            var certSection = section.GetSection(nameof(ProxyHttpClientOptions.ClientCertificate));

            X509Certificate2 clientCertificate = null;

            if (certSection?.Exists() is true)
            {
                clientCertificate = _certificateConfigLoader.LoadCertificate(certSection);
            }

            if (clientCertificate != null)
            {
                Certificates.AddLast(new WeakReference<X509Certificate2>(clientCertificate));
            }

            SslProtocols? sslProtocols = null;
            if (section.GetSection(nameof(ProxyHttpClientOptions.SslProtocols)) is IConfigurationSection sslProtocolsSection)
            {
                foreach (var protocolConfig in sslProtocolsSection.GetChildren().Select(s => Enum.Parse<SslProtocols>(s.Value)))
                {
                    sslProtocols = sslProtocols == null ? protocolConfig : sslProtocols | protocolConfig;
                }
            }

            return new ProxyHttpClientOptions
            {
                SslProtocols = sslProtocols,
                DangerousAcceptAnyServerCertificate = bool.TryParse(section[nameof(ProxyHttpClientOptions.DangerousAcceptAnyServerCertificate)], out var dangerousAcceptAnyCert) ? dangerousAcceptAnyCert : default,
                ClientCertificate = clientCertificate,
                MaxConnectionsPerServer = int.TryParse(section[nameof(ProxyHttpClientOptions.MaxConnectionsPerServer)], out var connectionsPerServer) ? connectionsPerServer : default
            };
        }

        private ProxyHttpRequestOptions CreateProxyRequestOptions(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            // Parse version only if it contains any characters; otherwise, leave it null.
            Version version = null;
            if (section[nameof(ProxyHttpRequestOptions.Version)] is string versionString && !string.IsNullOrEmpty(versionString))
            {
                version = Version.Parse(versionString + (versionString.Contains('.') ? "" : ".0"));
            }

            return new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.TryParse(section[nameof(ProxyHttpRequestOptions.RequestTimeout)], out var requestTimeout) ? requestTimeout : default,
                Version = version,
#if NET
                VersionPolicy = Enum.TryParse<HttpVersionPolicy>(section[nameof(ProxyHttpRequestOptions.VersionPolicy)], out var versionPolicy) ? versionPolicy : default,
#endif
            };
        }

        private static Destination CreateDestination(IConfigurationSection section)
        {
            if (section?.Exists() is false)
            {
                return null;
            }

            return new Destination
            {
                Address = section[nameof(Destination.Address)],
                Health = section[nameof(Destination.Health)],
                Metadata = CreateStringDictionary(section.GetSection(nameof(Destination.Metadata))),
            };
        }

        private static IDictionary<string, string> CreateStringDictionary(IConfigurationSection section)
        {
            if (section?.GetChildren() is var children && children?.Any() is false)
            {
                return null;
            }

            return children.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static string[] GetStringArray(IConfigurationSection section)
        {
            if (section?.GetChildren() is var children && children?.Any() is false)
            {
                return null;
            }

            return children.Select(s => s.Value).ToArray();
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

            private static readonly Action<ILogger, Exception> _configurationDataConversionFailed = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.ConfigurationDataConversionFailed,
                "Configuration data conversion failed.");

            public static void ErrorSignalingChange(ILogger logger, Exception exception)
            {
                _errorSignalingChange(logger, exception);
            }

            public static void LoadData(ILogger logger)
            {
                _loadData(logger, null);
            }

            public static void ConfigurationDataConversionFailed(ILogger logger, Exception exception)
            {
                _configurationDataConversionFailed(logger, exception);
            }
        }
    }
}
