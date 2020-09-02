// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Configuration.Contract;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Utilities.Tests;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Configuration
{
    public class ConfigurationConfigProviderTests
    {
        #region JSON test configuration

        private readonly ConfigurationData _validConfigurationData = new ConfigurationData()
        {
            Clusters =
                {
                {
                    "cluster1",
                        new ClusterData
                        {
                            Destinations = {
                                {
                                    "destinationA",
                                    new DestinationData { Address = "https://localhost:10000/destA", Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } } }
                                },
                                {
                                    "destinationB",
                                    new DestinationData { Address = "https://localhost:10000/destB", Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" } } }
                                }
                            },
                            CircuitBreakerData = new CircuitBreakerData { MaxConcurrentRequests = 2, MaxConcurrentRetries = 3 },
                            HealthCheckData = new HealthCheckData { Enabled = true, Interval = TimeSpan.FromSeconds(4), Path = "healthCheckPath", Port = 5, Timeout = TimeSpan.FromSeconds(6) },
                            LoadBalancing = new LoadBalancingData { Mode = "Random" },
                            PartitioningData = new ClusterPartitioningData { PartitionCount = 7, PartitioningAlgorithm = "SHA358", PartitionKeyExtractor = "partionKeyA" },
                            QuotaData = new QuotaData { Average = 8.5, Burst = 9.1 },
                            SessionAffinity = new SessionAffinityData
                            {
                                Enabled = true,
                                FailurePolicy = "Return503Error",
                                Mode = "Cookie",
                                Settings = new Dictionary<string, string> { { "affinity1-K1", "affinity1-V1" }, { "affinity1-K2", "affinity1-V2" } }
                            },
                            HttpClientData = new ProxyHttpClientData
                            {
                                SslProtocols = new List<SslProtocols> { SslProtocols.Tls11, SslProtocols.Tls12 },
                                MaxConnectionsPerServer = 10,
                                DangerousAcceptAnyServerCertificate = true,
                                ClientCertificate = new CertificateConfigData { Path = "mycert.pfx", Password = "myPassword1234" }
                            },
                            Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
                        }
                    },
                    {
                    "cluster2",
                        new ClusterData
                        {
                            Destinations = {
                                { "destinationC", new DestinationData { Address = "https://localhost:10001/destC" } },
                                { "destinationD", new DestinationData { Address = "https://localhost:10000/destB" } }
                            },
                            LoadBalancing = new LoadBalancingData { Mode = "RoundRobin" }
                        }
                    }
            },
                Routes = {
                new ProxyRouteData
                {
                    RouteId = "routeA",
                    ClusterId = "cluster1",
                    AuthorizationPolicy = "Default",
                    CorsPolicy = "Default",
                    Order = 1,
                    Match = { Hosts = new List<string> { "host-A" }, Methods = new List<string> { "GET", "POST", "DELETE" }, Path = "/apis/entities" },
                    Transforms = new List<IDictionary<string, string>>
                        {
                            new Dictionary<string, string> { { "RequestHeadersCopy", "true" }, { "PathRemovePrefix", "/apis" } }, new Dictionary<string, string> { { "PathPrefix", "/apis" } }
                        },
                    Metadata = new Dictionary<string, string> { { "routeA-K1", "routeA-V1" }, { "routeA-K2", "routeA-V2" } }
                },
                    new ProxyRouteData
                    {
                        RouteId = "routeB",
                        ClusterId = "cluster2",
                        Order = 2,
                        Match = { Hosts = new List<string> { "host-B" }, Methods = new List<string> { "GET" }, Path = "/apis/users" }
                    }
                }
        };

        private const string _validJsonConfig = @"
{
    ""Clusters"": {
        ""cluster1"": {
            ""CircuitBreakerData"": {
                ""MaxConcurrentRequests"": 2,
                ""MaxConcurrentRetries"": 3
            },
            ""QuotaData"": {
                ""Average"": 8.5,
                ""Burst"": 9.1
            },
            ""PartitioningData"": {
                ""PartitionCount"": 7,
                ""PartitionKeyExtractor"": ""partionKeyA"",
                ""PartitioningAlgorithm"": ""SHA358""
            },
            ""LoadBalancing"": {
                ""Mode"": ""Random""
            },
            ""SessionAffinity"": {
                ""Enabled"": true,
                ""Mode"": ""Cookie"",
                ""FailurePolicy"": ""Return503Error"",
                ""Settings"": {
                    ""affinity1-K1"": ""affinity1-V1"",
                    ""affinity1-K2"": ""affinity1-V2""
                }
            },
            ""HealthCheckData"": {
                ""Enabled"": true,
                ""Interval"": ""00:00:04"",
                ""Timeout"": ""00:00:06"",
                ""Port"": 5,
                ""Path"": ""healthCheckPath""
            },
            ""HttpClientData"": {
                ""SslProtocols"": [
                    768,
                    3072
                ],
                ""DangerousAcceptAnyServerCertificate"": true,
                ""ClientCertificate"": {
                    ""Path"": ""mycert.pfx"",
                    ""KeyPath"": null,
                    ""Password"": ""myPassword1234"",
                    ""Subject"": null,
                    ""Store"": null,
                    ""Location"": null,
                    ""AllowInvalid"": null
                },
                ""MaxConnectionsPerServer"": 10
            },
            ""Destinations"": {
                ""destinationA"": {
                    ""Address"": ""https://localhost:10000/destA"",
                    ""Metadata"": {
                        ""destA-K1"": ""destA-V1"",
                        ""destA-K2"": ""destA-V2""
                    }
                },
                ""destinationB"": {
                    ""Address"": ""https://localhost:10000/destB"",
                    ""Metadata"": {
                        ""destB-K1"": ""destB-V1"",
                        ""destB-K2"": ""destB-V2""
                    }
                }
            },
            ""Metadata"": {
                ""cluster1-K1"": ""cluster1-V1"",
                ""cluster1-K2"": ""cluster1-V2""
            }
        },
        ""cluster2"": {
            ""CircuitBreakerData"": null,
            ""QuotaData"": null,
            ""PartitioningData"": null,
            ""LoadBalancing"": {
                ""Mode"": ""RoundRobin""
            },
            ""SessionAffinity"": null,
            ""HealthCheckData"": null,
            ""HttpClientData"": null,
            ""Destinations"": {
                ""destinationC"": {
                    ""Address"": ""https://localhost:10001/destC"",
                    ""Metadata"": null
                },
                ""destinationD"": {
                    ""Address"": ""https://localhost:10000/destB"",
                    ""Metadata"": null
                }
            },
            ""Metadata"": null
        }
    },
    ""Routes"": [
        {
            ""RouteId"": ""routeA"",
            ""Match"": {
                ""Methods"": [
                    ""GET"",
                    ""POST"",
                    ""DELETE""
                ],
                ""Hosts"": [
                    ""host-A""
                ],
                ""Path"": ""/apis/entities""
            },
            ""Order"": 1,
            ""ClusterId"": ""cluster1"",
            ""AuthorizationPolicy"": ""Default"",
            ""CorsPolicy"": ""Default"",
            ""Metadata"": {
                ""routeA-K1"": ""routeA-V1"",
                ""routeA-K2"": ""routeA-V2""
            },
            ""Transforms"": [
                {
                    ""RequestHeadersCopy"": ""true"",
                    ""PathRemovePrefix"": ""/apis""
                },
                {
                    ""PathPrefix"": ""/apis""
                }
            ]
        },
        {
            ""RouteId"": ""routeB"",
            ""Match"": {
                ""Methods"": [
                    ""GET""
                ],
                ""Hosts"": [
                    ""host-B""
                ],
                ""Path"": ""/apis/users""
            },
            ""Order"": 2,
            ""ClusterId"": ""cluster2"",
            ""AuthorizationPolicy"": null,
            ""CorsPolicy"": null,
            ""Metadata"": null,
            ""Transforms"": null
        }
    ]
}
";

        #endregion

        [Fact]
        public void GetConfig_ValidSerializedConfiguration_ConvertToAbstractionsSuccessfully()
        {
            var builder = new ConfigurationBuilder();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(_validJsonConfig));
            var proxyConfig = builder.AddJsonStream(stream).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IProxyConfigProvider, ConfigurationConfigProvider>();
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            var certificate = TestResources.GetTestCertificate();
            certLoader.Setup(l => l.LoadCertificate(It.Is<CertificateConfigData>(o => o.Path == "mycert.pfx" && o.Password == "myPassword1234"))).Returns(certificate);
            services.AddSingleton(certLoader.Object);
            services.AddSingleton(new Mock<ILogger<ConfigurationConfigProvider>>().Object);
            services.Configure<ConfigurationData>(proxyConfig);
            var serviceProvider = services.BuildServiceProvider();

            var provider = serviceProvider.GetRequiredService<IProxyConfigProvider>();
            Assert.NotNull(provider);
            var abstractConfig = provider.GetConfig();

            VerifyValidAbstractConfig(_validConfigurationData, certificate, abstractConfig);
        }

        [Fact]
        public void GetConfig_ValidConfiguration_ConvertToAbstractionsSuccessfully()
        {
            var certificate = TestResources.GetTestCertificate();
            var provider = GetProvider(_validConfigurationData, "mycert.pfx", "myPassword1234", () => certificate);
            var abstractConfig = provider.GetConfig();

            VerifyValidAbstractConfig(_validConfigurationData, certificate, abstractConfig);
        }

        [Fact]
        public void GetConfig_FirstTime_CertificateLoadingThrewException_Throws()
        {
            var config = new ConfigurationData()
            {
                Clusters = {
                    {
                        "cluster1",
                        new ClusterData {
                            Destinations = { { "destinationA", new DestinationData { Address = "https://localhost:10001/destC" } } },
                            HttpClientData = new ProxyHttpClientData { ClientCertificate = new CertificateConfigData { Path = "mycert.pfx", Password = "123" }}
                        }
                    }
                },
                Routes = { new ProxyRouteData { RouteId = "routeA", ClusterId = "cluster1", Order = 1, Match = { Hosts = new List<string> { "host-B" } } } }
            };

            var provider = GetProvider(config, "mycert.pfx", "123", () => throw new FileNotFoundException());

            Assert.Throws<FileNotFoundException>(() => provider.GetConfig());
        }

        [Fact]
        public void GetConfig_SecondTime_CertificateLoadingThrewException_ErrorLogged()
        {
            var config = new ConfigurationData()
            {
                Clusters = {
                    {
                        "cluster1",
                        new ClusterData {
                            Destinations = { { "destinationA", new DestinationData { Address = "https://localhost:10001/destC" } } }
                        }
                    }
                },
                Routes = { new ProxyRouteData { RouteId = "routeA", ClusterId = "cluster1", Order = 1, Match = { Hosts = new List<string> { "host-B" } } } }
            };

            var logger = new Mock<ILogger<ConfigurationConfigProvider>>();
            logger.Setup(l => l.IsEnabled(LogLevel.Error)).Returns(true);
            var configMonitor = new Mock<IOptionsMonitor<ConfigurationData>>();
            configMonitor.SetupGet(m => m.CurrentValue).Returns(config);
            Action<ConfigurationData, string> onChangeCallback = (a, s) => { Assert.False(true, "OnChange method was not called."); };
            configMonitor.Setup(m => m.OnChange(It.IsAny<Action<ConfigurationData, string>>())).Callback((Action<ConfigurationData, string> a) => { onChangeCallback = a; });
            var provider = GetProvider(configMonitor.Object, "mycert.pfx", "123", () => throw new FileNotFoundException(), logger);

            var firstSnapshot = provider.GetConfig();
            Assert.NotNull(firstSnapshot);
            logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception, string>>()), Times.Never);

            config.Clusters["cluster1"].HttpClientData = new ProxyHttpClientData { ClientCertificate = new CertificateConfigData { Path = "mycert.pfx", Password = "123" } };

            onChangeCallback(config, null);
            var secondSnapshot = provider.GetConfig();
            Assert.Same(firstSnapshot, secondSnapshot);
            logger.Verify(l => l.Log(LogLevel.Error, EventIds.ConfigurationDataConversionFailed, It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        private void VerifyValidAbstractConfig(ConfigurationData validConfig, X509Certificate2 certificate, IProxyConfig abstractConfig)
        {
            Assert.NotNull(abstractConfig);
            Assert.Equal(2, abstractConfig.Clusters.Count);

            Assert.Single(abstractConfig.Clusters.Where(c => c.Id == "cluster1"));
            var abstractCluster1 = abstractConfig.Clusters.Single(c => c.Id == "cluster1");
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationA"].Address, abstractCluster1.Destinations["destinationA"].Address);
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationA"].Metadata, abstractCluster1.Destinations["destinationA"].Metadata);
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationB"].Address, abstractCluster1.Destinations["destinationB"].Address);
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationB"].Metadata, abstractCluster1.Destinations["destinationB"].Metadata);
            Assert.Equal(validConfig.Clusters["cluster1"].CircuitBreakerData.MaxConcurrentRequests, abstractCluster1.CircuitBreakerOptions.MaxConcurrentRequests);
            Assert.Equal(validConfig.Clusters["cluster1"].CircuitBreakerData.MaxConcurrentRetries, abstractCluster1.CircuitBreakerOptions.MaxConcurrentRetries);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckData.Enabled, abstractCluster1.HealthCheckOptions.Enabled);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckData.Interval, abstractCluster1.HealthCheckOptions.Interval);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckData.Path, abstractCluster1.HealthCheckOptions.Path);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckData.Port, abstractCluster1.HealthCheckOptions.Port);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckData.Timeout, abstractCluster1.HealthCheckOptions.Timeout);
            Assert.Equal(Abstractions.LoadBalancingMode.Random, abstractCluster1.LoadBalancing.Mode);
            Assert.Equal(validConfig.Clusters["cluster1"].PartitioningData.PartitionCount, abstractCluster1.PartitioningOptions.PartitionCount);
            Assert.Equal(validConfig.Clusters["cluster1"].PartitioningData.PartitioningAlgorithm, abstractCluster1.PartitioningOptions.PartitioningAlgorithm);
            Assert.Equal(validConfig.Clusters["cluster1"].PartitioningData.PartitionKeyExtractor, abstractCluster1.PartitioningOptions.PartitionKeyExtractor);
            Assert.Equal(validConfig.Clusters["cluster1"].QuotaData.Average, abstractCluster1.QuotaOptions.Average);
            Assert.Equal(validConfig.Clusters["cluster1"].QuotaData.Burst, abstractCluster1.QuotaOptions.Burst);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Enabled, abstractCluster1.SessionAffinity.Enabled);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.FailurePolicy, abstractCluster1.SessionAffinity.FailurePolicy);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Mode, abstractCluster1.SessionAffinity.Mode);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Settings, abstractCluster1.SessionAffinity.Settings);
            Assert.Same(certificate, abstractCluster1.HttpClientOptions.ClientCertificate);
            Assert.Equal(validConfig.Clusters["cluster1"].HttpClientData.MaxConnectionsPerServer, abstractCluster1.HttpClientOptions.MaxConnectionsPerServer);
            Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, abstractCluster1.HttpClientOptions.SslProtocols);
            Assert.Equal(validConfig.Clusters["cluster1"].HttpClientData.DangerousAcceptAnyServerCertificate, abstractCluster1.HttpClientOptions.DangerousAcceptAnyServerCertificate);
            Assert.Equal(validConfig.Clusters["cluster1"].Metadata, abstractCluster1.Metadata);

            Assert.Single(abstractConfig.Clusters.Where(c => c.Id == "cluster2"));
            var abstractCluster2 = abstractConfig.Clusters.Single(c => c.Id == "cluster2");
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationC"].Address, abstractCluster2.Destinations["destinationC"].Address);
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationC"].Metadata, abstractCluster2.Destinations["destinationC"].Metadata);
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationD"].Address, abstractCluster2.Destinations["destinationD"].Address);
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationD"].Metadata, abstractCluster2.Destinations["destinationD"].Metadata);
            Assert.Equal(Abstractions.LoadBalancingMode.RoundRobin, abstractCluster2.LoadBalancing.Mode);

            Assert.Equal(2, abstractConfig.Routes.Count);

            VerifyRoute(validConfig, abstractConfig, "routeA");
            VerifyRoute(validConfig, abstractConfig, "routeB");
        }

        private void VerifyRoute(ConfigurationData validConfig, IProxyConfig abstractConfig, string routeId)
        {
            var route = validConfig.Routes.Single(c => c.RouteId == routeId);
            Assert.Single(abstractConfig.Routes.Where(c => c.RouteId == routeId));
            var abstractRoute = abstractConfig.Routes.Single(c => c.RouteId == routeId);
            Assert.Equal(route.ClusterId, abstractRoute.ClusterId);
            Assert.Equal(route.Order, abstractRoute.Order);
            Assert.Equal(route.Match.Hosts, abstractRoute.Match.Hosts);
            Assert.Equal(route.Match.Methods, abstractRoute.Match.Methods);
            Assert.Equal(route.Match.Path, abstractRoute.Match.Path);
            Assert.Equal(route.Transforms, abstractRoute.Transforms);
        }

        private ConfigurationConfigProvider GetProvider(ConfigurationData rawConfig, string certPath, string certPassword, Func<X509Certificate2> certificateFunc)
        {
            var monitor = new Mock<IOptionsMonitor<ConfigurationData>>();
            monitor.SetupGet(m => m.CurrentValue).Returns(rawConfig);
            return GetProvider(monitor.Object, certPath, certPassword, certificateFunc, null);
        }

        private ConfigurationConfigProvider GetProvider(
            IOptionsMonitor<ConfigurationData> configMonitor,
            string certPath,
            string certPassword,
            Func<X509Certificate2> certificateFunc,
            Mock<ILogger<ConfigurationConfigProvider>> logger)
        {
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            certLoader.Setup(l => l.LoadCertificate(It.Is<CertificateConfigData>(o => o.Path == certPath && o.Password == certPassword))).Returns(certificateFunc);
            return new ConfigurationConfigProvider(logger?.Object ?? new Mock<ILogger<ConfigurationConfigProvider>>().Object, configMonitor, certLoader.Object);
        }
    }
}
