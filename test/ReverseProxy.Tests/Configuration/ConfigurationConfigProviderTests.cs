// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities.Tests;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Configuration
{
    public class ConfigurationConfigProviderTests
    {
        #region JSON test configuration

        private readonly ConfigurationSnapshot _validConfigurationData = new ConfigurationSnapshot()
        {
            Clusters =
            {
                {
                    new Cluster
                    {
                        Id = "cluster1",
                        Destinations = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase)
                        {
                            {
                                "destinationA",
                                new Destination
                                {
                                    Address = "https://localhost:10000/destA",
                                    Health = "https://localhost:20000/destA",
                                    Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
                                }
                            },
                            {
                                "destinationB",
                                new Destination
                                {
                                    Address = "https://localhost:10000/destB",
                                    Health = "https://localhost:20000/destB",
                                    Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" } }
                                }
                            }
                        },
                        HealthCheck = new HealthCheckOptions
                        {
                            Passive = new PassiveHealthCheckOptions
                            {
                                Enabled = true,
                                Policy = "FailureRate",
                                ReactivationPeriod = TimeSpan.FromMinutes(5)
                            },
                            Active = new ActiveHealthCheckOptions
                            {
                                Enabled = true,
                                Interval = TimeSpan.FromSeconds(4),
                                Timeout = TimeSpan.FromSeconds(6),
                                Policy = "Any5xxResponse",
                                Path = "healthCheckPath"
                            }
                        },
                        LoadBalancingPolicy = LoadBalancingPolicies.Random,
                        SessionAffinity = new SessionAffinityOptions
                        {
                            Enabled = true,
                            FailurePolicy = "Return503Error",
                            Mode = "Cookie",
                            Settings = new Dictionary<string, string> { { "affinity1-K1", "affinity1-V1" }, { "affinity1-K2", "affinity1-V2" } }
                        },
                        HttpClient = new ProxyHttpClientOptions
                        {
                            SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                            MaxConnectionsPerServer = 10,
                            DangerousAcceptAnyServerCertificate = true,
                            PropagateActivityContext = true,
#if NET
                            RequestHeaderEncoding = Encoding.UTF8
#endif
                        },
                        HttpRequest = new RequestProxyOptions()
                        {
                            Timeout = TimeSpan.FromSeconds(60),
                            Version = Version.Parse("1.0"),
#if NET
                            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
#endif
                        },
                        Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
                    }
                },
                {
                    new Cluster
                    {
                        Id = "cluster2",
                        Destinations = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "destinationC", new Destination { Address = "https://localhost:10001/destC" } },
                            { "destinationD", new Destination { Address = "https://localhost:10000/destB" } }
                        },
                        LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin
                    }
                }
            },
            Routes =
            {
                new ProxyRoute
                {
                    RouteId = "routeA",
                    ClusterId = "cluster1",
                    AuthorizationPolicy = "Default",
                    CorsPolicy = "Default",
                    Order = -1,
                    Match = new ProxyMatch
                    {
                        Hosts = new List<string> { "host-A" },
                        Methods = new List<string> { "GET", "POST", "DELETE" },
                        Path = "/apis/entities",
                        Headers = new[]
                        {
                            new RouteHeader
                            {
                                Name = "header1",
                                Values = new[] { "value1" },
                                IsCaseSensitive = true,
                                Mode = HeaderMatchMode.HeaderPrefix
                            }
                        }
                    },
                    Transforms = new[]
                    {
                        new Dictionary<string, string> { { "RequestHeadersCopy", "true" }, { "PathRemovePrefix", "/apis" } }, new Dictionary<string, string> { { "PathPrefix", "/apis" } }
                    },
                    Metadata = new Dictionary<string, string> { { "routeA-K1", "routeA-V1" }, { "routeA-K2", "routeA-V2" } }
                },
                new ProxyRoute
                {
                    RouteId = "routeB",
                    ClusterId = "cluster2",
                    Order = 2,
                    Match = new ProxyMatch
                    {
                        Hosts = new List<string> { "host-B" },
                        Methods = new List<string> { "GET" },
                        Path = "/apis/users",
                        Headers = new[]
                        {
                            new RouteHeader
                            {
                                Name = "header2",
                                Values = new[] { "value2" },
                                IsCaseSensitive = false,
                                Mode = HeaderMatchMode.ExactHeader
                            }
                        }
                    }
                }
            }
        };

        private const string _validJsonConfig = @"
{
    ""Clusters"": {
        ""cluster1"": {
            ""LoadBalancingPolicy"": ""Random"",
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
                ""Passive"": {
                    ""Enabled"": true,
                    ""Policy"": ""FailureRate"",
                    ""ReactivationPeriod"": ""00:05:00""
                },
                ""Active"": {
                    ""Enabled"": true,
                    ""Interval"": ""00:00:04"",
                    ""Timeout"": ""00:00:06"",
                    ""Policy"": ""Any5xxResponse"",
                    ""Path"": ""healthCheckPath""
                }
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
                ""MaxConnectionsPerServer"": 10,
                ""PropagateActivityContext"": true,
                ""RequestHeaderEncoding"": ""utf-8"",
            },
            ""HttpRequest"": {
                ""Timeout"": ""00:01:00"",
                ""Version"": ""1"",
                ""VersionPolicy"": ""RequestVersionExact""
            },
            ""Destinations"": {
                ""destinationA"": {
                    ""Address"": ""https://localhost:10000/destA"",
                    ""Health"": ""https://localhost:20000/destA"",
                    ""Metadata"": {
                        ""destA-K1"": ""destA-V1"",
                        ""destA-K2"": ""destA-V2""
                    }
                },
                ""destinationB"": {
                    ""Address"": ""https://localhost:10000/destB"",
                    ""Health"": ""https://localhost:20000/destB"",
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
            ""LoadBalancingPolicy"": ""RoundRobin"",
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
                ""Path"": ""/apis/entities"",
                ""Headers"": [
                  {
                    ""Name"": ""header1"",
                    ""Values"": [ ""value1"" ],
                    ""IsCaseSensitive"": true,
                    ""Mode"": ""HeaderPrefix""
                  }
                ]
            },
            ""Order"": -1,
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
                ""Path"": ""/apis/users"",
                ""Headers"": [
                  {
                    ""Name"": ""header2"",
                    ""Values"": [ ""value2"" ]
                  }
                ]
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
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            using var certificate = TestResources.GetTestCertificate();
            certLoader.Setup(l => l.LoadCertificate(It.Is<IConfigurationSection>(o => o["Path"] == "mycert.pfx" && o["Password"] == "myPassword1234"))).Returns(certificate);
            var logger = new Mock<ILogger<ConfigurationConfigProvider>>();

            var provider = new ConfigurationConfigProvider(logger.Object, proxyConfig, certLoader.Object);
            Assert.NotNull(provider);
            var abstractConfig = provider.GetConfig();

            VerifyValidAbstractConfig(_validConfigurationData, certificate, abstractConfig);
        }

        [Fact]
        public void GetConfig_ValidConfiguration_AllAbstractionsPropertiesAreSet()
        {
            var builder = new ConfigurationBuilder();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(_validJsonConfig));
            var proxyConfig = builder.AddJsonStream(stream).Build();
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            using var certificate = TestResources.GetTestCertificate();
            certLoader.Setup(l => l.LoadCertificate(It.Is<IConfigurationSection>(o => o["Path"] == "mycert.pfx" && o["Password"] == "myPassword1234"))).Returns(certificate);
            var logger = new Mock<ILogger<ConfigurationConfigProvider>>();

            var provider = new ConfigurationConfigProvider(logger.Object, proxyConfig, certLoader.Object);
            var abstractConfig = (ConfigurationSnapshot)provider.GetConfig();

            var abstractionsNamespace = typeof(Cluster).Namespace;
            // Removed incompletely filled out instances.
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

                        var type = e.GetType();
                        if (!type.IsArray && type.Namespace == abstractionsNamespace)
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
                foreach (var property in properties)
                {
                    VerifyFullyInitialized(property.GetValue(obj), $"{property.DeclaringType.Name}.{property.Name}");
                }
            }
        }

        [Fact]
        public void GetConfig_FirstTime_CertificateLoadingThrewException_Throws()
        {
            var builder = new ConfigurationBuilder();
            var proxyConfig = builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Clusters:cluster1:Destinations:destinationA:Address"] = "https://localhost:10001/destC",
                ["Clusters:cluster1:HttpClient:ClientCertificate:Path"] = "mycert.pfx",
                ["Routes:0:RouteId"] = "routeA",
                ["Routes:0:ClusterId"] = "cluster1",
                ["Routes:0:Order"] = "1",
                ["Routes:0:Match:Hosts:0"] = "host-B",
            }).Build();
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            using var certificate = TestResources.GetTestCertificate();
            certLoader.Setup(l => l.LoadCertificate(It.IsAny<IConfigurationSection>())).Throws(new FileNotFoundException());
            var logger = new Mock<ILogger<ConfigurationConfigProvider>>();
            var provider = new ConfigurationConfigProvider(logger.Object, proxyConfig, certLoader.Object);
            Assert.ThrowsAny<FileNotFoundException>(() => provider.GetConfig());
        }

        [Fact]
        public void GetConfig_SecondTime_CertificateLoadingThrewException_ErrorLogged()
        {
            var builder = new ConfigurationBuilder();
            var proxyConfig = builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Clusters:cluster1:Destinations:destinationA:Address"] = "https://localhost:10001/destC",
                ["Routes:0:RouteId"] = "routeA",
                ["Routes:0:ClusterId"] = "cluster1",
                ["Routes:0:Order"] = "1",
                ["Routes:0:Match:Hosts:0"] = "host-B",
            }).Build();
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            using var certificate = TestResources.GetTestCertificate();
            certLoader.Setup(l => l.LoadCertificate(It.IsAny<IConfigurationSection>())).Throws(new FileNotFoundException());
            var logger = new Mock<ILogger<ConfigurationConfigProvider>>();
            logger.Setup(l => l.IsEnabled(LogLevel.Error)).Returns(true);
            var provider = new ConfigurationConfigProvider(logger.Object, proxyConfig, certLoader.Object);

            var firstSnapshot = provider.GetConfig();
            logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception, string>>()), Times.Never);

            // Add configuration entry here and trigger a change
            proxyConfig["Clusters:cluster1:HttpClient:ClientCertificate:Path"] = "mycert.pfx";

            TriggerOnChange(proxyConfig);

            var secondSnapshot = provider.GetConfig();
            Assert.Same(firstSnapshot, secondSnapshot);
            logger.Verify(l => l.Log(LogLevel.Error, EventIds.ConfigurationDataConversionFailed, It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public void CachedCertificateIsDisposed_RemoveItFromCache()
        {
            var builder = new ConfigurationBuilder();
            var proxyConfig = builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Clusters:cluster1:Destinations:destinationA:Address"] = "https://localhost:10001/destC",
                ["Clusters:cluster1:HttpClient:ClientCertificate:Path"] = "testCert.pfx",
                ["Routes:0:RouteId"] = "routeA",
                ["Routes:0:ClusterId"] = "cluster1",
                ["Routes:0:Order"] = "1",
                ["Routes:0:Match:Hosts:0"] = "host-B",
            }).Build();
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            using var certificate = TestResources.GetTestCertificate();
            certLoader.Setup(l => l.LoadCertificate(It.IsAny<IConfigurationSection>())).Returns(() => TestResources.GetTestCertificate());
            var logger = new Mock<ILogger<ConfigurationConfigProvider>>();
            logger.Setup(l => l.IsEnabled(LogLevel.Error)).Returns(true);
            var provider = new ConfigurationConfigProvider(logger.Object, proxyConfig, certLoader.Object);

            // Get several certificates.
            var certificateConfig = new List<X509Certificate2>();
            for (var i = 0; i < 5; i++)
            {
                certificateConfig.AddRange(provider.GetConfig().Clusters.Select(c => c.HttpClient.ClientCertificate));
                if (i < 4)
                {
                    TriggerOnChange(proxyConfig);
                }
            }

            // Verify cache contents match the configuration objects.
            var cachedCertificates = GetCachedCertificates(provider);
            Assert.Equal(certificateConfig.Count, cachedCertificates.Length);
            for (var i = 0; i < certificateConfig.Count; i++)
            {
                Assert.Same(certificateConfig[i], cachedCertificates[i]);
            }

            // Get several certificates.
            certificateConfig[1].Dispose();
            certificateConfig[3].Dispose();

            // Trigger cache compaction.
            TriggerOnChange(proxyConfig);

            // Verify disposed certificates were purged out.
            cachedCertificates = GetCachedCertificates(provider);
            Assert.Equal(4, cachedCertificates.Length);
            Assert.Same(certificateConfig[0], cachedCertificates[0]);
            Assert.Same(certificateConfig[2], cachedCertificates[1]);
            Assert.Same(certificateConfig[4], cachedCertificates[2]);
        }

        private void TriggerOnChange(IConfigurationRoot configurationRoot)
        {
            // This method is protected so we use reflection to trigger it. The alternative is to wrap or implement
            // a custom configuration provider and expose OnReload as a public method
            var reload = typeof(ConfigurationProvider).GetMethod("OnReload", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(reload);

            foreach (var provider in configurationRoot.Providers)
            {
                if (provider is ConfigurationProvider configProvider)
                {
                    reload.Invoke(configProvider, Array.Empty<object>());
                }
            }
        }

        private X509Certificate2[] GetCachedCertificates(ConfigurationConfigProvider provider)
        {
            return provider.Certificates.Select(r =>
            {
                Assert.True(r.TryGetTarget(out var certificate));
                return certificate;
            }).ToArray();
        }

        private void VerifyValidAbstractConfig(IProxyConfig validConfig, X509Certificate2 certificate, IProxyConfig abstractConfig)
        {
            Assert.NotNull(abstractConfig);
            Assert.Equal(2, abstractConfig.Clusters.Count);

            var cluster1 = validConfig.Clusters.First(c => c.Id == "cluster1");
            Assert.Single(abstractConfig.Clusters.Where(c => c.Id == "cluster1"));
            var abstractCluster1 = abstractConfig.Clusters.Single(c => c.Id == "cluster1");
            Assert.Equal(cluster1.Destinations["destinationA"].Address, abstractCluster1.Destinations["destinationA"].Address);
            Assert.Equal(cluster1.Destinations["destinationA"].Health, abstractCluster1.Destinations["destinationA"].Health);
            Assert.Equal(cluster1.Destinations["destinationA"].Metadata, abstractCluster1.Destinations["destinationA"].Metadata);
            Assert.Equal(cluster1.Destinations["destinationB"].Address, abstractCluster1.Destinations["destinationB"].Address);
            Assert.Equal(cluster1.Destinations["destinationB"].Health, abstractCluster1.Destinations["destinationB"].Health);
            Assert.Equal(cluster1.Destinations["destinationB"].Metadata, abstractCluster1.Destinations["destinationB"].Metadata);
            Assert.Equal(cluster1.HealthCheck.Passive.Enabled, abstractCluster1.HealthCheck.Passive.Enabled);
            Assert.Equal(cluster1.HealthCheck.Passive.Policy, abstractCluster1.HealthCheck.Passive.Policy);
            Assert.Equal(cluster1.HealthCheck.Passive.ReactivationPeriod, abstractCluster1.HealthCheck.Passive.ReactivationPeriod);
            Assert.Equal(cluster1.HealthCheck.Active.Enabled, abstractCluster1.HealthCheck.Active.Enabled);
            Assert.Equal(cluster1.HealthCheck.Active.Interval, abstractCluster1.HealthCheck.Active.Interval);
            Assert.Equal(cluster1.HealthCheck.Active.Timeout, abstractCluster1.HealthCheck.Active.Timeout);
            Assert.Equal(cluster1.HealthCheck.Active.Policy, abstractCluster1.HealthCheck.Active.Policy);
            Assert.Equal(cluster1.HealthCheck.Active.Path, abstractCluster1.HealthCheck.Active.Path);
            Assert.Equal(LoadBalancingPolicies.Random, abstractCluster1.LoadBalancingPolicy);
            Assert.Equal(cluster1.SessionAffinity.Enabled, abstractCluster1.SessionAffinity.Enabled);
            Assert.Equal(cluster1.SessionAffinity.FailurePolicy, abstractCluster1.SessionAffinity.FailurePolicy);
            Assert.Equal(cluster1.SessionAffinity.Mode, abstractCluster1.SessionAffinity.Mode);
            Assert.Equal(cluster1.SessionAffinity.Settings, abstractCluster1.SessionAffinity.Settings);
            Assert.Same(certificate, abstractCluster1.HttpClient.ClientCertificate);
            Assert.Equal(cluster1.HttpClient.MaxConnectionsPerServer, abstractCluster1.HttpClient.MaxConnectionsPerServer);
            Assert.Equal(cluster1.HttpClient.PropagateActivityContext, abstractCluster1.HttpClient.PropagateActivityContext);
            Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, abstractCluster1.HttpClient.SslProtocols);
#if NET
            Assert.Equal(Encoding.UTF8, abstractCluster1.HttpClient.RequestHeaderEncoding);
#endif
            Assert.Equal(cluster1.HttpRequest.Timeout, abstractCluster1.HttpRequest.Timeout);
            Assert.Equal(HttpVersion.Version10, abstractCluster1.HttpRequest.Version);
#if NET
            Assert.Equal(cluster1.HttpRequest.VersionPolicy, abstractCluster1.HttpRequest.VersionPolicy);
#endif
            Assert.Equal(cluster1.HttpClient.DangerousAcceptAnyServerCertificate, abstractCluster1.HttpClient.DangerousAcceptAnyServerCertificate);
            Assert.Equal(cluster1.Metadata, abstractCluster1.Metadata);

            var cluster2 = validConfig.Clusters.First(c => c.Id == "cluster2");
            Assert.Single(abstractConfig.Clusters.Where(c => c.Id == "cluster2"));
            var abstractCluster2 = abstractConfig.Clusters.Single(c => c.Id == "cluster2");
            Assert.Equal(cluster2.Destinations["destinationC"].Address, abstractCluster2.Destinations["destinationC"].Address);
            Assert.Equal(cluster2.Destinations["destinationC"].Metadata, abstractCluster2.Destinations["destinationC"].Metadata);
            Assert.Equal(cluster2.Destinations["destinationD"].Address, abstractCluster2.Destinations["destinationD"].Address);
            Assert.Equal(cluster2.Destinations["destinationD"].Metadata, abstractCluster2.Destinations["destinationD"].Metadata);
            Assert.Equal(LoadBalancingPolicies.RoundRobin, abstractCluster2.LoadBalancingPolicy);

            Assert.Equal(2, abstractConfig.Routes.Count);

            VerifyRoute(validConfig, abstractConfig, "routeA");
            VerifyRoute(validConfig, abstractConfig, "routeB");
        }

        private void VerifyRoute(IProxyConfig validConfig, IProxyConfig abstractConfig, string routeId)
        {
            var route = validConfig.Routes.Single(c => c.RouteId == routeId);
            Assert.Single(abstractConfig.Routes.Where(c => c.RouteId == routeId));
            var abstractRoute = abstractConfig.Routes.Single(c => c.RouteId == routeId);
            Assert.Equal(route.ClusterId, abstractRoute.ClusterId);
            Assert.Equal(route.Order, abstractRoute.Order);
            Assert.Equal(route.Match.Hosts, abstractRoute.Match.Hosts);
            Assert.Equal(route.Match.Methods, abstractRoute.Match.Methods);
            Assert.Equal(route.Match.Path, abstractRoute.Match.Path);
            var header = route.Match.Headers.Single();
            var expectedHeader = abstractRoute.Match.Headers.Single();
            Assert.Equal(header.Name, expectedHeader.Name);
            Assert.Equal(header.Mode, expectedHeader.Mode);
            Assert.Equal(header.IsCaseSensitive, expectedHeader.IsCaseSensitive);
            // Skipping header.Value/s because it's a fuzzy match
            Assert.Equal(route.Transforms, abstractRoute.Transforms);
        }
    }
}
