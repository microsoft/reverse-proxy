// <copyright file="LabelsParserTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
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
                { "YARP.Backend.LoadBalancing.Mode", "LeastRequests" },
                { "YARP.Backend.SessionAffinity.Enabled", "true" },
                { "YARP.Backend.SessionAffinity.Mode", "Cookie" },
                { "YARP.Backend.SessionAffinity.FailurePolicy", "Return503Error" },
                { "YARP.Backend.SessionAffinity.Settings.ParameterA", "ValueA" },
                { "YARP.Backend.SessionAffinity.Settings.ParameterB", "ValueB" },
                { "YARP.Backend.HttpRequest.Timeout", "17" },
                { "YARP.Backend.HttpRequest.Version", "1.1" },
#if NET
                { "YARP.Backend.HttpRequest.VersionPolicy", "RequestVersionExact" },
#endif
                { "YARP.Backend.HealthCheck.Active.Enabled", "true" },
                { "YARP.Backend.HealthCheck.Active.Interval", "5" },
                { "YARP.Backend.HealthCheck.Active.Timeout", "6" },
                { "YARP.Backend.HealthCheck.Active.Policy", "MyActiveHealthPolicy" },
                { "YARP.Backend.HealthCheck.Active.Path", "/api/health" },
                { "YARP.Backend.HealthCheck.Passive.Enabled", "true" },
                { "YARP.Backend.HealthCheck.Passive.Policy", "MyPassiveHealthPolicy" },
                { "YARP.Backend.HealthCheck.Passive.ReactivationPeriod", "7" },
                { "YARP.Backend.Metadata.Foo", "Bar" },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            var expectedCluster = new Cluster
            {
                Id = "MyCoolClusterId",
                LoadBalancing = new LoadBalancingOptions
                {
                    Mode = LoadBalancingMode.LeastRequests
                },
                SessionAffinity = new SessionAffinityOptions
                {
                    Enabled = true,
                    Mode = SessionAffinityConstants.Modes.Cookie,
                    FailurePolicy = SessionAffinityConstants.AffinityFailurePolicies.Return503Error,
                    Settings = new Dictionary<string, string>
                    {
                        { "ParameterA", "ValueA" },
                        { "ParameterB", "ValueB" }
                    }
                },
                HttpRequest = new ProxyHttpRequestOptions
                {
                    Timeout = TimeSpan.FromSeconds(17),
                    Version = new Version(1, 1),
#if NET
                    VersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact
#endif
                },
                HealthCheck = new HealthCheckOptions
                {
                    Active = new ActiveHealthCheckOptions
                    {
                        Enabled = true,
                        Interval = TimeSpan.FromSeconds(5),
                        Timeout = TimeSpan.FromSeconds(6),
                        Path = "/api/health",
                        Policy = "MyActiveHealthPolicy"
                    },
                    Passive = new PassiveHealthCheckOptions
                    {
                        Enabled = true,
                        Policy = "MyPassiveHealthPolicy",
                        ReactivationPeriod = TimeSpan.FromSeconds(7)
                    }
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
                SessionAffinity = new SessionAffinityOptions(),
                HttpRequest = new ProxyHttpRequestOptions(),
                HealthCheck = new HealthCheckOptions
                {
                    Active = new ActiveHealthCheckOptions(),
                    Passive = new PassiveHealthCheckOptions()
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
                { "YARP.Backend.HealthCheck.Active.Enabled", label },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            cluster.HealthCheck.Active.Enabled.Should().Be(expected);
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
                { "YARP.Backend.HealthCheck.Active.Enabled", label },
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
                { "YARP.Backend.HealthCheck.Active.Interval", "5" },
            };

            // Act
            var cluster = LabelsParser.BuildCluster(_testServiceName, labels);

            // Assert
            cluster.Id.Should().Be(_testServiceName.ToString());
        }

        [Theory]
        [InlineData("YARP.Backend.HealthCheck.Active.Interval", "1S")]
        [InlineData("YARP.Backend.HealthCheck.Active.Timeout", "foobar")]
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
                { "YARP.Routes.MyRoute.Order", "2" },
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
                    Order = 2,
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
                    Order = LabelsParser.DefaultRouteOrder,
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
                    Order = LabelsParser.DefaultRouteOrder,
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
                { "YARP.Routes.MyRoute.Order", "2" },
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
                    Order = 2,
                    ClusterId = _testServiceName.ToString(),
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Fact]
        public void BuildRoutes_MissingHost_Works()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Routes.MyRoute.Path", "/{**catchall}" },
            };

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
                        Path = "/{**catchall}",
                    },
                    ClusterId = _testServiceName.ToString(),
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Fact]
        public void BuildRoutes_InvalidOrder_Throws()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Order", "this is no number" },
            };

            // Act
            Func<List<ProxyRoute>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            func.Should()
                .Throw<ConfigException>()
                .WithMessage("Could not convert label YARP.Routes.MyRoute.Order='this is no number' *");
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
                { $"YARP.Routes.{routeName}.Order", "2" },
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
                    Order = 2,
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
                { "YARP.Routes.MyRoute.Order", "2" },
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
                    Order = 2,
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
                { "YARP.Routes.MyRoute.Order", "1" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
                { "YARP.Routes.CoolRoute.Hosts", "example.net" },
                { "YARP.Routes.CoolRoute.Order", "2" },
                { "YARP.Routes.EvenCoolerRoute.Hosts", "example.org" },
                { "YARP.Routes.EvenCoolerRoute.Order", "3" },
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
                    Order = 1,
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
                    Order = 2,
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
                    Order = 3,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>(),
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }
    }
}
