// <copyright file="LabelsParserTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ReverseProxy.Abstractions;
using Xunit;

namespace Microsoft.ReverseProxy.ServiceFabric.Tests
{
    public class LabelsParserTests
    {
        private static readonly Uri _testServiceName = new Uri("fabric:/App1/Svc1");

        [Fact]
        public void BuildCluster_CompleteLabels_Works()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Enable", "true" },
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.CircuitBreaker.MaxConcurrentRequests", "42" },
                { "YARP.Backend.CircuitBreaker.MaxConcurrentRetries", "5" },
                { "YARP.Backend.Quota.Average", "1.2" },
                { "YARP.Backend.Quota.Burst", "2.3" },
                { "YARP.Backend.Partitioning.Count", "5" },
                { "YARP.Backend.Partitioning.KeyExtractor", "Header('x-ms-organization-id')" },
                { "YARP.Backend.Partitioning.Algorithm", "SHA256" },
                { "YARP.Backend.Healthcheck.Enabled", "true" },
                { "YARP.Backend.Healthcheck.Interval", "5" },
                { "YARP.Backend.Healthcheck.Timeout", "5" },
                { "YARP.Backend.Healthcheck.Port", "8787" },
                { "YARP.Backend.Healthcheck.Path", "/api/health" },
                { "YARP.Backend.Metadata.Foo", "Bar" },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            var expectedCluster = new Cluster
            {
                Id = "MyCoolClusterId",
                CircuitBreakerOptions = new CircuitBreakerOptions
                {
                    MaxConcurrentRequests = 42,
                    MaxConcurrentRetries = 5,
                },
                QuotaOptions = new QuotaOptions
                {
                    Average = 1.2,
                    Burst = 2.3,
                },
                PartitioningOptions = new ClusterPartitioningOptions
                {
                    PartitionCount = 5,
                    PartitionKeyExtractor = "Header('x-ms-organization-id')",
                    PartitioningAlgorithm = "SHA256",
                },
                LoadBalancing = new LoadBalancingOptions(),
                HealthCheckOptions = new HealthCheckOptions
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(5),
                    Timeout = TimeSpan.FromSeconds(5),
                    Port = 8787,
                    Path = "/api/health",
                },
                Metadata = new Dictionary<string, string>
                {
                    { "Foo", "Bar" },
                },
            };
            cluster.Should().BeEquivalentTo(expectedCluster);
        }

        [Fact]
        public void BuildCluster_IncompleteLabels_UsesDefaultValues()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            var expectedCluster = new Cluster
            {
                Id = "MyCoolClusterId",
                CircuitBreakerOptions = new CircuitBreakerOptions
                {
                    MaxConcurrentRequests = LabelsParser.DefaultCircuitbreakerMaxConcurrentRequests,
                    MaxConcurrentRetries = LabelsParser.DefaultCircuitbreakerMaxConcurrentRetries,
                },
                QuotaOptions = new QuotaOptions
                {
                    Average = LabelsParser.DefaultQuotaAverage,
                    Burst = LabelsParser.DefaultQuotaBurst,
                },
                PartitioningOptions = new ClusterPartitioningOptions
                {
                    PartitionCount = LabelsParser.DefaultPartitionCount,
                    PartitionKeyExtractor = LabelsParser.DefaultPartitionKeyExtractor,
                    PartitioningAlgorithm = LabelsParser.DefaultPartitioningAlgorithm,
                },
                LoadBalancing = new LoadBalancingOptions(),
                HealthCheckOptions = new HealthCheckOptions
                {
                    Enabled = false,
                    Interval = TimeSpan.Zero,
                    Timeout = TimeSpan.Zero,
                    Port = null,
                    Path = null,
                },
                Metadata = new Dictionary<string, string>(),
            };
            cluster.Should().BeEquivalentTo(expectedCluster);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("TRUE", true)]
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("FALSE", false)]
        public void BuildCluster_HealthCheckOptions_Enabled_Valid(string label, bool expected)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.Healthcheck.Enabled", label },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            cluster.HealthCheckOptions.Enabled.Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void BuildCluster_HealthCheckOptions_Enabled_Invalid(string label)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.Healthcheck.Enabled", label },
            };

            // Act
            Action action = () => LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            action.Should().Throw<ConfigException>();
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("443", 443)]
        [InlineData("0x1bc", 444)]
        [InlineData("  80 ", 80)]
        [InlineData("65000", 65000)]
        public void BuildCluster_HealthCheckOptions_Port_Valid(string label, int? expected)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.Healthcheck.Port", label },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            cluster.HealthCheckOptions.Port.Should().Be(expected);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("123a")]
        public void BuildCluster_HealthCheckOptions_Port_Invalid(string label)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.Healthcheck.Port", label },
            };

            // Act
            Action action = () => LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            action.Should().Throw<ConfigException>();
        }

        [Fact]
        public void BuildCluster_MissingBackendId_UsesServiceName()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.Quota.Burst", "2.3" },
                { "YARP.Backend.Partitioning.Count", "5" },
                { "YARP.Backend.Partitioning.KeyExtractor", "Header('x-ms-organization-id')" },
                { "YARP.Backend.Partitioning.Algorithm", "SHA256" },
                { "YARP.Backend.Healthcheck.Interval", "5" },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            cluster.Id.Should().Be(_testServiceName.ToString());
        }

        [Theory]
        [InlineData("YARP.Backend.CircuitBreaker.MaxConcurrentRequests", "this is no number boi")]
        [InlineData("YARP.Backend.CircuitBreaker.MaxConcurrentRetries", "P=NP")]
        [InlineData("YARP.Backend.Quota.Average", "average should be a double")]
        [InlineData("YARP.Backend.Quota.Burst", "")]
        [InlineData("YARP.Backend.Partitioning.Count", "$#%+@`~áéÉ")]
        [InlineData("YARP.Backend.Healthcheck.Interval", "1S")]
        [InlineData("YARP.Backend.Healthcheck.Timeout", "foobar")]
        [InlineData("YARP.Backend.Healthcheck.Port", "should be an int")]
        public void BuildCluster_InvalidValues_Throws(string key, string invalidValue)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { key, invalidValue },
            };

            // Act
            Func<Cluster> func = () => LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            func.Should().Throw<ConfigException>().WithMessage($"Could not convert label {key}='{invalidValue}' *");
        }

        [Fact]
        public void BuildRoutes_SingleRoute_Works()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
            };

            // Act
            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            var expectedRoutes = new List<ProxyRoute>
            {
                new ProxyRoute
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match =
                    {
                        Hosts = new[] { "example.com" },
                    },
                    Priority = 2,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Foo", "Bar" },
                    },
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Fact]
        public void BuildRoutes_IncompleteRoute_UsesDefaults()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
            };

            // Act
            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            var expectedRoutes = new List<ProxyRoute>
            {
                new ProxyRoute
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match =
                    {
                        Hosts = new[] { "example.com" },
                    },
                    Priority = LabelsParser.DefaultRoutePriority,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        /// <summary>
        /// The LabelParser is not expected to invoke route parsing logic, and should treat the objects as plain data containers.
        /// </summary>
        [Fact]
        public void BuildRoutes_SingleRouteWithSemanticallyInvalidRule_WorksAndDoesNotThrow()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "'this invalid thing" },
            };

            // Act
            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            var expectedRoutes = new List<ProxyRoute>
            {
                new ProxyRoute
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match =
                    {
                        Hosts = new[] { "'this invalid thing" },
                    },
                    Priority = LabelsParser.DefaultRoutePriority,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void BuildRoutes_MissingBackendId_UsesServiceName(int scenario)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
            };

            if (scenario == 1)
            {
                labels.Add("YARP.Backend.BackendId", string.Empty);
            }

            // Act
            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            var expectedRoutes = new List<ProxyRoute>
            {
                new ProxyRoute
                {
                    RouteId = $"{Uri.EscapeDataString(_testServiceName.ToString())}:MyRoute",
                    Match =
                    {
                        Hosts = new[] { "example.com" },
                    },
                    Priority = 2,
                    ClusterId = _testServiceName.ToString(),
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Fact]
        public void BuildRoutes_MissingHost_Throws()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Priority", "2" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
            };

            // Act
            Func<List<ProxyRoute>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            func.Should().Throw<ConfigException>().WithMessage("Missing 'YARP.Routes.MyRoute.Hosts'.");
        }

        [Fact]
        public void BuildRoutes_InvalidPriority_Throws()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "this is no number" },
            };

            // Act
            Func<List<ProxyRoute>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            func.Should()
                .Throw<ConfigException>()
                .WithMessage("Could not convert label YARP.Routes.MyRoute.Priority='this is no number' *");
        }

        [Theory]
        [InlineData("justcharacters")]
        [InlineData("UppercaseCharacters")]
        [InlineData("numbers1234")]
        [InlineData("Under_Score")]
        [InlineData("Hyphen-Hyphen")]
        public void BuildRoutes_ValidRouteName_Works(string routeName)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { $"YARP.Routes.{routeName}.Hosts", "example.com" },
                { $"YARP.Routes.{routeName}.Priority", "2" },
            };

            // Act
            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            var expectedRoutes = new List<ProxyRoute>
            {
                new ProxyRoute
                {
                    RouteId = $"MyCoolClusterId:{routeName}",
                    Match =
                    {
                        Hosts = new[] { "example.com" },
                    },
                    Priority = 2,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Theory]
        [InlineData("YARP.Routes..Priority", "that was an empty route name")]
        [InlineData("YARP.Routes..Hosts", "that was an empty route name")]
        [InlineData("YARP.Routes.  .Hosts", "that was an empty route name")]
        [InlineData("YARP.Routes..", "that was an empty route name")]
        [InlineData("YARP.Routes...", "that was an empty route name")]
        [InlineData("YARP.Routes.FunnyChars!.Hosts", "some value")]
        [InlineData("YARP.Routes.'FunnyChars'.Priority", "some value")]
        [InlineData("YARP.Routes.FunnyChárs.Metadata", "some value")]
        [InlineData("YARP.Routes.Funny+Chars.Hosts", "some value")]
        public void BuildRoutes_InvalidRouteName_Throws(string invalidKey, string value)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
            };
            labels[invalidKey] = value;

            // Act
            Func<List<ProxyRoute>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            func.Should()
                .Throw<ConfigException>()
                .WithMessage($"Invalid route name '*', should only contain alphanumerical characters, underscores or hyphens.");
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("NotEven.TheNamespace", "some value")]
        [InlineData("YARP.", "some value")]
        [InlineData("Routes.", "some value")]
        [InlineData("YARP.Routes.", "some value")]
        [InlineData("YARP.Routes", "some value")]
        [InlineData("YARP..Routes.", "some value")]
        [InlineData("YARP.....Routes.", "some value")]
        public void BuildRoutes_InvalidLabelKeys_IgnoresAndDoesNotThrow(string invalidKey, string value)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
            };
            labels[invalidKey] = value;

            // Act
            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            var expectedRoutes = new List<ProxyRoute>
            {
                new ProxyRoute
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match =
                    {
                        Hosts = new[] { "example.com" },
                    },
                    Priority = 2,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Foo", "Bar" },
                    },
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Fact]
        public void BuildRoutes_MultipleRoutes_Works()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Path", "v2/{**rest}" },
                { "YARP.Routes.MyRoute.Priority", "1" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
                { "YARP.Routes.CoolRoute.Hosts", "example.net" },
                { "YARP.Routes.CoolRoute.Priority", "2" },
                { "YARP.Routes.EvenCoolerRoute.Hosts", "example.org" },
                { "YARP.Routes.EvenCoolerRoute.Priority", "3" },
            };

            // Act
            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            var expectedRoutes = new List<ProxyRoute>
            {
                new ProxyRoute
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match =
                    {
                        Hosts = new[] { "example.com" },
                        Path = "v2/{**rest}",
                    },
                    Priority = 1,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string> { { "Foo", "Bar" } },
                },
                new ProxyRoute
                {
                    RouteId = "MyCoolClusterId:CoolRoute",
                    Match =
                    {
                        Hosts = new[] { "example.net" },
                    },
                    Priority = 2,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>(),
                },
                new ProxyRoute
                {
                    RouteId = "MyCoolClusterId:EvenCoolerRoute",
                    Match =
                    {
                        Hosts = new[] { "example.org" },
                    },
                    Priority = 3,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }
    }
}
