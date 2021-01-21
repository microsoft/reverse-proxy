// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Microsoft.ReverseProxy.Service.Proxy;

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
            var destinations = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase);
            foreach (var destination in section.GetSection(nameof(Cluster.Destinations)).GetChildren())
            {
                destinations.Add(destination.Key, CreateDestination(destination));
            }

            return new Cluster
            {
                Id = section.Key,
                LoadBalancingPolicy = section[nameof(Cluster.LoadBalancingPolicy)],
                SessionAffinity = CreateSessionAffinityOptions(section.GetSection(nameof(Cluster.SessionAffinity))),
                HealthCheck = CreateHealthCheckOptions(section.GetSection(nameof(Cluster.HealthCheck))),
                HttpClient = CreateProxyHttpClientOptions(section.GetSection(nameof(Cluster.HttpClient))),
                HttpRequest = CreateProxyRequestOptions(section.GetSection(nameof(Cluster.HttpRequest))),
                Metadata = section.GetSection(nameof(Cluster.Metadata)).ReadOnlyStringDictionary(),
                Destinations = destinations,
            };
        }

        private static ProxyRoute CreateRoute(IConfigurationSection section)
        {
            return new ProxyRoute
            {
                RouteId = section[nameof(ProxyRoute.RouteId)],
                Order = section.ReadInt32(nameof(ProxyRoute.Order)),
                ClusterId = section[nameof(ProxyRoute.ClusterId)],
                AuthorizationPolicy = section[nameof(ProxyRoute.AuthorizationPolicy)],
                CorsPolicy = section[nameof(ProxyRoute.CorsPolicy)],
                Metadata = section.GetSection(nameof(ProxyRoute.Metadata)).ReadOnlyStringDictionary(),
                Transforms = CreateTransforms(section.GetSection(nameof(ProxyRoute.Transforms))),
                Match = CreateProxyMatch(section.GetSection(nameof(ProxyRoute.Match))),
            };
        }

        private static IReadOnlyList<IReadOnlyDictionary<string, string>> CreateTransforms(IConfigurationSection section)
        {
            if (section.GetChildren() is var children && !children.Any())
            {
                return null;
            }

            return children.Select(subSection => new ReadOnlyDictionary<string, string>(
                    subSection.GetChildren().ToDictionary(d => d.Key, d => d.Value, StringComparer.OrdinalIgnoreCase)))
                .ToList<IReadOnlyDictionary<string, string>>().AsReadOnly();
        }

        private static ProxyMatch CreateProxyMatch(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new ProxyMatch()
            {
                Methods = section.GetSection(nameof(ProxyMatch.Methods)).ReadStringArray(),
                Hosts = section.GetSection(nameof(ProxyMatch.Hosts)).ReadStringArray(),
                Path = section[nameof(ProxyMatch.Path)],
                Headers = CreateRouteHeaders(section.GetSection(nameof(ProxyMatch.Headers))),
            };
        }

        private static IReadOnlyList<RouteHeader> CreateRouteHeaders(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return section.GetChildren().Select(data => CreateRouteHeader(data)).ToArray();
        }

        private static RouteHeader CreateRouteHeader(IConfigurationSection section)
        {
            return new RouteHeader()
            {
                Name = section[nameof(RouteHeader.Name)],
                Values = section.GetSection(nameof(RouteHeader.Values)).ReadStringArray(),
                Mode = section.ReadEnum<HeaderMatchMode>(nameof(RouteHeader.Mode)) ?? HeaderMatchMode.ExactHeader,
                IsCaseSensitive = section.ReadBool(nameof(RouteHeader.IsCaseSensitive)) ?? false,
            };
        }

        private static SessionAffinityOptions CreateSessionAffinityOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new SessionAffinityOptions
            {
                Enabled = section.ReadBool(nameof(SessionAffinityOptions.Enabled)) ?? false,
                Mode = section[nameof(SessionAffinityOptions.Mode)],
                FailurePolicy = section[nameof(SessionAffinityOptions.FailurePolicy)],
                Settings = section.GetSection(nameof(SessionAffinityOptions.Settings)).ReadOnlyStringDictionary()
            };
        }

        private static HealthCheckOptions CreateHealthCheckOptions(IConfigurationSection section)
        {
            if (!section.Exists())
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
            if (!section.Exists())
            {
                return null;
            }

            return new PassiveHealthCheckOptions
            {
                Enabled = section.ReadBool(nameof(PassiveHealthCheckOptions.Enabled)) ?? false,
                Policy = section[nameof(PassiveHealthCheckOptions.Policy)],
                ReactivationPeriod = section.ReadTimeSpan(nameof(PassiveHealthCheckOptions.ReactivationPeriod))
            };
        }

        private static ActiveHealthCheckOptions CreateActiveHealthCheckOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new ActiveHealthCheckOptions
            {
                Enabled = section.ReadBool(nameof(ActiveHealthCheckOptions.Enabled)) ?? false,
                Interval = section.ReadTimeSpan(nameof(ActiveHealthCheckOptions.Interval)),
                Timeout = section.ReadTimeSpan(nameof(ActiveHealthCheckOptions.Timeout)),
                Policy = section[nameof(ActiveHealthCheckOptions.Policy)],
                Path = section[nameof(ActiveHealthCheckOptions.Path)]
            };
        }

        private ProxyHttpClientOptions CreateProxyHttpClientOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            var certSection = section.GetSection(nameof(ProxyHttpClientOptions.ClientCertificate));

            X509Certificate2 clientCertificate = null;

            if (certSection.Exists())
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
                DangerousAcceptAnyServerCertificate = section.ReadBool(nameof(ProxyHttpClientOptions.DangerousAcceptAnyServerCertificate)),
                ClientCertificate = clientCertificate,
                MaxConnectionsPerServer = section.ReadInt32(nameof(ProxyHttpClientOptions.MaxConnectionsPerServer)),
                PropagateActivityContext = section.ReadBool(nameof(ProxyHttpClientOptions.PropagateActivityContext))
            };
        }

        private static RequestProxyOptions CreateProxyRequestOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new RequestProxyOptions
            {
                Timeout = section.ReadTimeSpan(nameof(RequestProxyOptions.Timeout)),
                Version = section.ReadVersion(nameof(RequestProxyOptions.Version)),
#if NET
                VersionPolicy = section.ReadEnum<HttpVersionPolicy>(nameof(RequestProxyOptions.VersionPolicy)),
#endif
            };
        }

        private static Destination CreateDestination(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new Destination
            {
                Address = section[nameof(Destination.Address)],
                Health = section[nameof(Destination.Health)],
                Metadata = section.GetSection(nameof(Destination.Metadata)).ReadOnlyStringDictionary(),
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
