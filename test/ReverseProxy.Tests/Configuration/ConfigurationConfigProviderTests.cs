// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                        CircuitBreaker = new CircuitBreakerData { MaxConcurrentRequests = 2, MaxConcurrentRetries = 3 },
                        HealthCheck = new HealthCheckData { Enabled = true, Interval = TimeSpan.FromSeconds(4), Path = "healthCheckPath", Port = 5, Timeout = TimeSpan.FromSeconds(6) },
                        LoadBalancing = new LoadBalancingData { Mode = "Random" },
                        Partitioning = new ClusterPartitioningData { PartitionCount = 7, PartitioningAlgorithm = "SHA358", PartitionKeyExtractor = "partionKeyA" },
                        Quota = new QuotaData { Average = 8.5, Burst = 9.1 },
                        SessionAffinity = new SessionAffinityData
                        {
                            Enabled = true,
                            FailurePolicy = "Return503Error",
                            Mode = "Cookie",
                            Settings = new Dictionary<string, string> { { "affinity1-K1", "affinity1-V1" }, { "affinity1-K2", "affinity1-V2" } }
                        },
                        HttpClient = new ProxyHttpClientData
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
                        Destinations =
                        {
                            { "destinationC", new DestinationData { Address = "https://localhost:10001/destC" } },
                            { "destinationD", new DestinationData { Address = "https://localhost:10000/destB" } }
                        },
                        LoadBalancing = new LoadBalancingData { Mode = "RoundRobin" }
                    }
                }
            },
            Routes =
            {
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
            ""CircuitBreaker"": {
                ""MaxConcurrentRequests"": 2,
                ""MaxConcurrentRetries"": 3
            },
            ""Quota"": {
                ""Average"": 8.5,
                ""Burst"": 9.1
            },
            ""Partitioning"": {
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
            ""HealthCheck"": {
                ""Enabled"": true,
                ""Interval"": ""00:00:04"",
                ""Timeout"": ""00:00:06"",
                ""Port"": 5,
                ""Path"": ""healthCheckPath""
            },
            ""HttpClient"": {
                ""SslProtocols"": [
                    ""Tls11"",
                    ""Tls12""
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
            ""CircuitBreaker"": null,
            ""Quota"": null,
            ""Partitioning"": null,
            ""LoadBalancing"": {
                ""Mode"": ""RoundRobin""
            },
            ""SessionAffinity"": null,
            ""HealthCheck"": null,
            ""HttpClient"": null,
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
        public void GetConfig_ValidConfiguration_AllAbstractionsPropertiesAreSet()
        {
            var certificate = TestResources.GetTestCertificate();
            var abstractionsNamespace = typeof(Abstractions.Cluster).Namespace;
            var provider = GetProvider(_validConfigurationData, "mycert.pfx", "myPassword1234", () => certificate);
            var abstractConfig = (ConfigurationSnapshot) provider.GetConfig();
            //Removed incompletely filled out instances.
            abstractConfig.Clusters = abstractConfig.Clusters.Where(c => c.Id == "cluster1").ToList();
            abstractConfig.Routes = abstractConfig.Routes.Where(r => r.RouteId == "routeA").ToList();

            VerifyAllPropertiesAreSet(abstractConfig);

            void VerifyFullyInitialized(object obj, string name)
            {
                switch (obj)
                {
                    case null:
                        Assert.True(false, $"Property {name} is not initialized.");
                        break;
                    case Enum m:
                        Assert.NotEqual(0, (int)(object)m);
                        break;
                    case string str:
                        Assert.NotEmpty(str);
                        break;
                    case ValueType v:
                        var equals = Equals(Activator.CreateInstance(v.GetType()), v);
                        Assert.False(equals, $"Property {name} is not initialized.");
                        if (v.GetType().Namespace == abstractionsNamespace)
                        {
                            VerifyAllPropertiesAreSet(v);
                        }
                        break;
                    case IDictionary d:
                        Assert.NotEmpty(d);
                        foreach (var value in d.Values)
                        {
                            VerifyFullyInitialized(value, name);
                        }
                        break;
                    case IEnumerable e:
                        Assert.NotEmpty(e);
                        foreach (var item in e)
                        {
                            VerifyFullyInitialized(item, name);
                        }

                        if (e.GetType().Namespace == abstractionsNamespace)
                        {
                            VerifyAllPropertiesAreSet(e);
                        }
                        break;
                    case object o:
                        if (o.GetType().Namespace == abstractionsNamespace)
                        {
                            VerifyAllPropertiesAreSet(o);
                        }
                        break;
                }
            }

            void VerifyAllPropertiesAreSet(object obj)
            {
                var properties = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Cast<PropertyInfo>();
                foreach(var property in properties)
                {
                    VerifyFullyInitialized(property.GetValue(obj), $"{property.DeclaringType.Name}.{property.Name}");
                }
            }
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
                            HttpClient = new ProxyHttpClientData { ClientCertificate = new CertificateConfigData { Path = "mycert.pfx", Password = "123" }}
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

            config.Clusters["cluster1"].HttpClient = new ProxyHttpClientData { ClientCertificate = new CertificateConfigData { Path = "mycert.pfx", Password = "123" } };

            onChangeCallback(config, null);
            var secondSnapshot = provider.GetConfig();
            Assert.Same(firstSnapshot, secondSnapshot);
            logger.Verify(l => l.Log(LogLevel.Error, EventIds.ConfigurationDataConversionFailed, It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public void CachedCertificateIsDisposed_RemoveItFromCache()
        {
            var config = new ConfigurationData()
            {
                Clusters = {
                    {
                        "cluster1",
                        new ClusterData {
                            Destinations = { { "destinationA", new DestinationData { Address = "https://localhost:10001/destC" } } },
                            HttpClient = new ProxyHttpClientData { ClientCertificate = new CertificateConfigData { Path = "testCert.pfx" }}
                        }
                    }
                },
                Routes = { new ProxyRouteData { RouteId = "routeA", ClusterId = "cluster1", Order = 1, Match = { Hosts = new List<string> { "host-B" } } } }
            };

            var configMonitor = new Mock<IOptionsMonitor<ConfigurationData>>();
            Action<ConfigurationData, string> onChangeCallback = (a, s) => { Assert.False(true, "OnChange method was not called."); };
            configMonitor.SetupGet(m => m.CurrentValue).Returns(config);
            configMonitor.Setup(m => m.OnChange(It.IsAny<Action<ConfigurationData, string>>())).Callback((Action<ConfigurationData, string> a) => { onChangeCallback = a; });
            var provider = GetProvider(configMonitor.Object, "testCert.pfx", null, () => TestResources.GetTestCertificate(), null);

            // Get several certificates.
            var certificateConfig = new List<X509Certificate2>();
            for (var i = 0; i < 5; i++)
            {
                certificateConfig.AddRange(provider.GetConfig().Clusters.Select(c => c.HttpClient.ClientCertificate));
                if (i < 4)
                {
                    onChangeCallback(config, null);
                }
            }

            // Verify cache contents match the configuration objects.
            var cachedCertificates = GetCachedCertificates(provider);
            Assert.Equal(certificateConfig.Count, cachedCertificates.Length);
            for(var i = 0; i < certificateConfig.Count; i++)
            {
                Assert.Same(certificateConfig[i], cachedCertificates[i]);
            }

            // Get several certificates.
            certificateConfig[1].Dispose();
            certificateConfig[3].Dispose();

            // Trigger cache compaction.
            onChangeCallback(config, null);

            // Verify disposed certificates were purged out.
            cachedCertificates = GetCachedCertificates(provider);
            Assert.Equal(4, cachedCertificates.Length);
            Assert.Same(certificateConfig[0], cachedCertificates[0]);
            Assert.Same(certificateConfig[2], cachedCertificates[1]);
            Assert.Same(certificateConfig[4], cachedCertificates[2]);
        }

        private X509Certificate2[] GetCachedCertificates(ConfigurationConfigProvider provider)
        {
            var certficatesField = typeof(ConfigurationConfigProvider).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(f => f.FieldType == typeof(LinkedList<WeakReference<X509Certificate2>>));
            var cache = (LinkedList<WeakReference<X509Certificate2>>)certficatesField.GetValue(provider);
            return cache.Select(r =>
            {
                Assert.True(r.TryGetTarget(out var certificate));
                return certificate;
            }).ToArray();
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
            Assert.Equal(validConfig.Clusters["cluster1"].CircuitBreaker.MaxConcurrentRequests, abstractCluster1.CircuitBreaker.MaxConcurrentRequests);
            Assert.Equal(validConfig.Clusters["cluster1"].CircuitBreaker.MaxConcurrentRetries, abstractCluster1.CircuitBreaker.MaxConcurrentRetries);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheck.Enabled, abstractCluster1.HealthCheck.Enabled);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheck.Interval, abstractCluster1.HealthCheck.Interval);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheck.Path, abstractCluster1.HealthCheck.Path);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheck.Port, abstractCluster1.HealthCheck.Port);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheck.Timeout, abstractCluster1.HealthCheck.Timeout);
            Assert.Equal(Abstractions.LoadBalancingMode.Random, abstractCluster1.LoadBalancing.Mode);
            Assert.Equal(validConfig.Clusters["cluster1"].Partitioning.PartitionCount, abstractCluster1.Partitioning.PartitionCount);
            Assert.Equal(validConfig.Clusters["cluster1"].Partitioning.PartitioningAlgorithm, abstractCluster1.Partitioning.PartitioningAlgorithm);
            Assert.Equal(validConfig.Clusters["cluster1"].Partitioning.PartitionKeyExtractor, abstractCluster1.Partitioning.PartitionKeyExtractor);
            Assert.Equal(validConfig.Clusters["cluster1"].Quota.Average, abstractCluster1.Quota.Average);
            Assert.Equal(validConfig.Clusters["cluster1"].Quota.Burst, abstractCluster1.Quota.Burst);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Enabled, abstractCluster1.SessionAffinity.Enabled);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.FailurePolicy, abstractCluster1.SessionAffinity.FailurePolicy);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Mode, abstractCluster1.SessionAffinity.Mode);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Settings, abstractCluster1.SessionAffinity.Settings);
            Assert.Same(certificate, abstractCluster1.HttpClient.ClientCertificate);
            Assert.Equal(validConfig.Clusters["cluster1"].HttpClient.MaxConnectionsPerServer, abstractCluster1.HttpClient.MaxConnectionsPerServer);
            Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, abstractCluster1.HttpClient.SslProtocols);
            Assert.Equal(validConfig.Clusters["cluster1"].HttpClient.DangerousAcceptAnyServerCertificate, abstractCluster1.HttpClient.DangerousAcceptAnyServerCertificate);
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
