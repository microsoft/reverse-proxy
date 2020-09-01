// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
        [Fact]
        public void GetConfig_ValidConfiguration_ConvertToAbstractionsSuccessfully()
        {
            var validConfig = new ConfigurationOptions()
            {
                Clusters =
                {
                    {
                        "cluster1",
                        new Cluster
                        {
                            Destinations = {
                                {
                                    "destinationA",
                                    new Destination { Address = "https://localhost:10000/destA", Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } } }
                                },
                                {
                                    "destinationB",
                                    new Destination { Address = "https://localhost:10000/destB", Metadata = new Dictionary<string, string> { { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" } } }
                                }
                            },
                            CircuitBreakerOptions = new CircuitBreakerOptions { MaxConcurrentRequests = 2, MaxConcurrentRetries = 3 },
                            HealthCheckOptions = new HealthCheckOptions { Enabled = true, Interval = TimeSpan.FromSeconds(4), Path = "healthCheckPath", Port = 5, Timeout = TimeSpan.FromSeconds(6) },
                            LoadBalancing = new LoadBalancingOptions { Mode = "Random" },
                            PartitioningOptions = new ClusterPartitioningOptions { PartitionCount = 7, PartitioningAlgorithm = "SHA358", PartitionKeyExtractor = "partionKeyA" },
                            QuotaOptions = new QuotaOptions { Average = 8.5, Burst = 9.1 },
                            SessionAffinity = new SessionAffinityOptions {
                                Enabled = true,
                                FailurePolicy = "Return503Error",
                                Mode = "Cookie",
                                Settings = new Dictionary<string, string> { { "affinity1-K1", "affinity1-V1" }, { "affinity1-K2", "affinity1-V2" } }
                            },
                            HttpClientOptions = new ProxyHttpClientOptions {
                                SslProtocols = new List<SslProtocols> { SslProtocols.Tls11, SslProtocols.Tls12 },
                                MaxConnectionsPerServer = 10,
                                ValidateRemoteCertificate = true,
                                ClientCertificate = new CertificateConfigOptions { Path = "mycert.pfx", Password = "myPassword1234" }
                            },
                            Metadata = new Dictionary<string, string> { { "cluster1-K1", "cluster1-V1" }, { "cluster1-K2", "cluster1-V2" } }
                        }
                    },
                    {
                        "cluster2",
                        new Cluster
                        {
                            Destinations = {
                                { "destinationC", new Destination { Address = "https://localhost:10001/destC" } },
                                { "destinationD", new Destination { Address = "https://localhost:10000/destB" } }
                            },
                            LoadBalancing = new LoadBalancingOptions { Mode = "RoundRobin" }
                        }
                    }
                },
                Routes = {
                    new ProxyRoute
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
                    new ProxyRoute
                    {
                        RouteId = "routeB",
                        ClusterId = "cluster2",
                        Order = 2,
                        Match = { Hosts = new List<string> { "host-B" }, Methods = new List<string> { "GET" }, Path = "/apis/users" }
                    }
                }
            };

            var certificate = TestResources.GetTestCertificate();
            var provider = GetProvider(validConfig, "mycert.pfx", "myPassword1234", () => certificate);
            var abstractConfig = provider.GetConfig();

            Assert.NotNull(abstractConfig);
            Assert.Equal(2, abstractConfig.Clusters.Count);

            Assert.Single(abstractConfig.Clusters.Where(c => c.Id == "cluster1"));
            var abstractCluster1 = abstractConfig.Clusters.Single(c => c.Id == "cluster1");
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationA"].Address, abstractCluster1.Destinations["destinationA"].Address);
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationA"].Metadata, abstractCluster1.Destinations["destinationA"].Metadata);
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationB"].Address, abstractCluster1.Destinations["destinationB"].Address);
            Assert.Equal(validConfig.Clusters["cluster1"].Destinations["destinationB"].Metadata, abstractCluster1.Destinations["destinationB"].Metadata);
            Assert.Equal(validConfig.Clusters["cluster1"].CircuitBreakerOptions.MaxConcurrentRequests, abstractCluster1.CircuitBreakerOptions.MaxConcurrentRequests);
            Assert.Equal(validConfig.Clusters["cluster1"].CircuitBreakerOptions.MaxConcurrentRetries, abstractCluster1.CircuitBreakerOptions.MaxConcurrentRetries);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckOptions.Enabled, abstractCluster1.HealthCheckOptions.Enabled);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckOptions.Interval, abstractCluster1.HealthCheckOptions.Interval);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckOptions.Path, abstractCluster1.HealthCheckOptions.Path);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckOptions.Port, abstractCluster1.HealthCheckOptions.Port);
            Assert.Equal(validConfig.Clusters["cluster1"].HealthCheckOptions.Timeout, abstractCluster1.HealthCheckOptions.Timeout);
            Assert.Equal(Abstractions.LoadBalancingMode.Random, abstractCluster1.LoadBalancing.Mode);
            Assert.Equal(validConfig.Clusters["cluster1"].PartitioningOptions.PartitionCount, abstractCluster1.PartitioningOptions.PartitionCount);
            Assert.Equal(validConfig.Clusters["cluster1"].PartitioningOptions.PartitioningAlgorithm, abstractCluster1.PartitioningOptions.PartitioningAlgorithm);
            Assert.Equal(validConfig.Clusters["cluster1"].PartitioningOptions.PartitionKeyExtractor, abstractCluster1.PartitioningOptions.PartitionKeyExtractor);
            Assert.Equal(validConfig.Clusters["cluster1"].QuotaOptions.Average, abstractCluster1.QuotaOptions.Average);
            Assert.Equal(validConfig.Clusters["cluster1"].QuotaOptions.Burst, abstractCluster1.QuotaOptions.Burst);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Enabled, abstractCluster1.SessionAffinity.Enabled);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.FailurePolicy, abstractCluster1.SessionAffinity.FailurePolicy);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Mode, abstractCluster1.SessionAffinity.Mode);
            Assert.Equal(validConfig.Clusters["cluster1"].SessionAffinity.Settings, abstractCluster1.SessionAffinity.Settings);
            Assert.Same(certificate, abstractCluster1.HttpClientOptions.ClientCertificate);
            Assert.Equal(validConfig.Clusters["cluster1"].HttpClientOptions.MaxConnectionsPerServer, abstractCluster1.HttpClientOptions.MaxConnectionsPerServer);
            Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, abstractCluster1.HttpClientOptions.SslProtocols);
            Assert.Equal(validConfig.Clusters["cluster1"].HttpClientOptions.ValidateRemoteCertificate, abstractCluster1.HttpClientOptions.ValidateRemoteCertificate);
            Assert.Equal(validConfig.Clusters["cluster1"].Metadata, abstractCluster1.Metadata);

            Assert.Single(abstractConfig.Clusters.Where(c => c.Id == "cluster2"));
            var abstractCluster2= abstractConfig.Clusters.Single(c => c.Id == "cluster2");
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationC"].Address, abstractCluster2.Destinations["destinationC"].Address);
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationC"].Metadata, abstractCluster2.Destinations["destinationC"].Metadata);
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationD"].Address, abstractCluster2.Destinations["destinationD"].Address);
            Assert.Equal(validConfig.Clusters["cluster2"].Destinations["destinationD"].Metadata, abstractCluster2.Destinations["destinationD"].Metadata);
            Assert.Equal(Abstractions.LoadBalancingMode.RoundRobin, abstractCluster2.LoadBalancing.Mode);

            Assert.Equal(2, abstractConfig.Routes.Count);

            VerifyRoute(validConfig, abstractConfig, "routeA");
            VerifyRoute(validConfig, abstractConfig, "routeB");
        }

        [Fact]
        public void GetConfig_CertificateLoadingThrewException_Throws()
        {
            var config = new ConfigurationOptions()
            {
                Clusters = {
                    {
                        "cluster1",
                        new Cluster {
                            Destinations = { { "destinationA", new Destination { Address = "https://localhost:10001/destC" } } },
                            HttpClientOptions = new ProxyHttpClientOptions { ClientCertificate = new CertificateConfigOptions { Path = "mycert.pfx", Password = "123" }}
                        }
                    }
                },
                Routes = { new ProxyRoute { RouteId = "routeA", ClusterId = "cluster1", Order = 1, Match = { Hosts = new List<string> { "host-B" } } } }
            };

            var provider = GetProvider(config, "mycert.pfx", "123", () => throw new FileNotFoundException());

            Assert.Throws<FileNotFoundException>(() => provider.GetConfig());
        }

        private void VerifyRoute(ConfigurationOptions validConfig, IProxyConfig abstractConfig, string routeId)
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

        private ConfigurationConfigProvider GetProvider(ConfigurationOptions rawConfig, string certPath, string certPassword, Func<X509Certificate2> certificateFunc)
        {
            var monitor = new Mock<IOptionsMonitor<ConfigurationOptions>>();
            monitor.SetupGet(m => m.CurrentValue).Returns(rawConfig);
            var certLoader = new Mock<ICertificateConfigLoader>(MockBehavior.Strict);
            certLoader.Setup(l => l.LoadCertificate(It.Is<CertificateConfigOptions>(o => o.Path == certPath && o.Password == certPassword))).Returns(certificateFunc);
            return new ConfigurationConfigProvider(new Mock<ILogger<ConfigurationConfigProvider>>().Object, monitor.Object, certLoader.Object);
        }
    }
}
