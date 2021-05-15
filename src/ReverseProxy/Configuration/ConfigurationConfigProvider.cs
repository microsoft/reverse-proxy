// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Service;
using Yarp.ReverseProxy.Service.Proxy;

namespace Yarp.ReverseProxy.Configuration
{
    /// <summary>
    /// Reacts to configuration changes and applies configurations to the Reverse Proxy core.
    /// When configs are loaded from appsettings.json, this takes care of hot updates
    /// when appsettings.json is modified on disk.
    /// </summary>
    internal sealed class ConfigurationConfigProvider : IProxyConfigProvider, IDisposable
    {
        private readonly object _lockObject = new object();
        private readonly ILogger<ConfigurationConfigProvider> _logger;
        private readonly IConfiguration _configuration;
        private ConfigurationSnapshot _snapshot;
        private CancellationTokenSource _changeToken;
        private bool _disposed;
        private IDisposable _subscription;

        public ConfigurationConfigProvider(
            ILogger<ConfigurationConfigProvider> logger,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

        private ClusterConfig CreateCluster(IConfigurationSection section)
        {
            var destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var destination in section.GetSection(nameof(ClusterConfig.Destinations)).GetChildren())
            {
                destinations.Add(destination.Key, CreateDestination(destination));
            }

            return new ClusterConfig
            {
                ClusterId = section.Key,
                LoadBalancingPolicy = section[nameof(ClusterConfig.LoadBalancingPolicy)],
                SessionAffinity = CreateSessionAffinityOptions(section.GetSection(nameof(ClusterConfig.SessionAffinity))),
                HealthCheck = CreateHealthCheckOptions(section.GetSection(nameof(ClusterConfig.HealthCheck))),
                HttpClient = CreateProxyHttpClientOptions(section.GetSection(nameof(ClusterConfig.HttpClient))),
                HttpRequest = CreateProxyRequestOptions(section.GetSection(nameof(ClusterConfig.HttpRequest))),
                Metadata = section.GetSection(nameof(ClusterConfig.Metadata)).ReadStringDictionary(),
                Destinations = destinations,
            };
        }

        private static RouteConfig CreateRoute(IConfigurationSection section)
        {
            return new RouteConfig
            {
                RouteId = section.Key,
                Order = section.ReadInt32(nameof(RouteConfig.Order)),
                ClusterId = section[nameof(RouteConfig.ClusterId)],
                AuthorizationPolicy = section[nameof(RouteConfig.AuthorizationPolicy)],
                CorsPolicy = section[nameof(RouteConfig.CorsPolicy)],
                Metadata = section.GetSection(nameof(RouteConfig.Metadata)).ReadStringDictionary(),
                Transforms = CreateTransforms(section.GetSection(nameof(RouteConfig.Transforms))),
                Match = CreateRouteMatch(section.GetSection(nameof(RouteConfig.Match))),
            };
        }

        private static IReadOnlyList<IReadOnlyDictionary<string, string>> CreateTransforms(IConfigurationSection section)
        {
            if (section.GetChildren() is var children && !children.Any())
            {
                return null;
            }

            return children.Select(subSection =>
                    subSection.GetChildren().ToDictionary(d => d.Key, d => d.Value, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        private static RouteMatch CreateRouteMatch(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new RouteMatch()
            {
                Methods = section.GetSection(nameof(RouteMatch.Methods)).ReadStringArray(),
                Hosts = section.GetSection(nameof(RouteMatch.Hosts)).ReadStringArray(),
                Path = section[nameof(RouteMatch.Path)],
                Headers = CreateRouteHeaders(section.GetSection(nameof(RouteMatch.Headers))),
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

        private static SessionAffinityConfig CreateSessionAffinityOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new SessionAffinityConfig
            {
                Enabled = section.ReadBool(nameof(SessionAffinityConfig.Enabled)),
                Mode = section[nameof(SessionAffinityConfig.Mode)],
                FailurePolicy = section[nameof(SessionAffinityConfig.FailurePolicy)],
                AffinityKeyName = section[nameof(SessionAffinityConfig.AffinityKeyName)],
                Cookie = CreateSessionAffinityCookieConfig(section.GetSection(nameof(SessionAffinityConfig.Cookie)))
            };
        }

        private static SessionAffinityCookieConfig CreateSessionAffinityCookieConfig(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new SessionAffinityCookieConfig
            {
                Path = section[nameof(SessionAffinityCookieConfig.Path)],
                SameSite = section.ReadEnum<SameSiteMode>(nameof(SessionAffinityCookieConfig.SameSite)),
                HttpOnly = section.ReadBool(nameof(SessionAffinityCookieConfig.HttpOnly)),
                MaxAge = section.ReadTimeSpan(nameof(SessionAffinityCookieConfig.MaxAge)),
                Domain = section[nameof(SessionAffinityCookieConfig.Domain)],
                IsEssential = section.ReadBool(nameof(SessionAffinityCookieConfig.IsEssential)),
                SecurePolicy = section.ReadEnum<CookieSecurePolicy>(nameof(SessionAffinityCookieConfig.SecurePolicy)),
                Expiration = section.ReadTimeSpan(nameof(SessionAffinityCookieConfig.Expiration))
            };
        }

        private static HealthCheckConfig CreateHealthCheckOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new HealthCheckConfig
            {
                Passive = CreatePassiveHealthCheckOptions(section.GetSection(nameof(HealthCheckConfig.Passive))),
                Active = CreateActiveHealthCheckOptions(section.GetSection(nameof(HealthCheckConfig.Active)))
            };
        }

        private static PassiveHealthCheckConfig CreatePassiveHealthCheckOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new PassiveHealthCheckConfig
            {
                Enabled = section.ReadBool(nameof(PassiveHealthCheckConfig.Enabled)),
                Policy = section[nameof(PassiveHealthCheckConfig.Policy)],
                ReactivationPeriod = section.ReadTimeSpan(nameof(PassiveHealthCheckConfig.ReactivationPeriod))
            };
        }

        private static ActiveHealthCheckConfig CreateActiveHealthCheckOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new ActiveHealthCheckConfig
            {
                Enabled = section.ReadBool(nameof(ActiveHealthCheckConfig.Enabled)),
                Interval = section.ReadTimeSpan(nameof(ActiveHealthCheckConfig.Interval)),
                Timeout = section.ReadTimeSpan(nameof(ActiveHealthCheckConfig.Timeout)),
                Policy = section[nameof(ActiveHealthCheckConfig.Policy)],
                Path = section[nameof(ActiveHealthCheckConfig.Path)]
            };
        }

        private HttpClientConfig CreateProxyHttpClientOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            SslProtocols? sslProtocols = null;
            if (section.GetSection(nameof(HttpClientConfig.SslProtocols)) is IConfigurationSection sslProtocolsSection)
            {
                foreach (var protocolConfig in sslProtocolsSection.GetChildren().Select(s => Enum.Parse<SslProtocols>(s.Value, ignoreCase: true)))
                {
                    sslProtocols = sslProtocols == null ? protocolConfig : sslProtocols | protocolConfig;
                }
            }

            WebProxyConfig webProxy;
            var webProxySection = section.GetSection(nameof(HttpClientConfig.WebProxy));
            if (webProxySection.Exists())
            {
                webProxy = new WebProxyConfig()
                {
                    Address = webProxySection.ReadUri(nameof(WebProxyConfig.Address)),
                    BypassOnLocal = webProxySection.ReadBool(nameof(WebProxyConfig.BypassOnLocal)),
                    UseDefaultCredentials = webProxySection.ReadBool(nameof(WebProxyConfig.UseDefaultCredentials))
                };
            }
            else
            {
                webProxy = null;
            }

            return new HttpClientConfig
            {
                SslProtocols = sslProtocols,
                DangerousAcceptAnyServerCertificate = section.ReadBool(nameof(HttpClientConfig.DangerousAcceptAnyServerCertificate)),
                MaxConnectionsPerServer = section.ReadInt32(nameof(HttpClientConfig.MaxConnectionsPerServer)),
#if NET
                EnableMultipleHttp2Connections = section.ReadBool(nameof(HttpClientConfig.EnableMultipleHttp2Connections)),
                RequestHeaderEncoding = section[nameof(HttpClientConfig.RequestHeaderEncoding)],
#endif
                ActivityContextHeaders = section.ReadEnum<ActivityContextHeaders>(nameof(HttpClientConfig.ActivityContextHeaders)),
                WebProxy = webProxy
            };
        }

        private static RequestProxyConfig CreateProxyRequestOptions(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new RequestProxyConfig
            {
                Timeout = section.ReadTimeSpan(nameof(RequestProxyConfig.Timeout)),
                Version = section.ReadVersion(nameof(RequestProxyConfig.Version)),
#if NET
                VersionPolicy = section.ReadEnum<HttpVersionPolicy>(nameof(RequestProxyConfig.VersionPolicy)),
#endif
            };
        }

        private static DestinationConfig CreateDestination(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return null;
            }

            return new DestinationConfig
            {
                Address = section[nameof(DestinationConfig.Address)],
                Health = section[nameof(DestinationConfig.Health)],
                Metadata = section.GetSection(nameof(DestinationConfig.Metadata)).ReadStringDictionary(),
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
