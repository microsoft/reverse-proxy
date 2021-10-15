// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Authentication;
using FluentAssertions;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.SessionAffinity;

namespace Yarp.ReverseProxy.ServiceFabric.Tests
{
    public class LabelsParserTests
    {
        private static readonly Uri _testServiceName = new Uri("fabric:/App1/Svc1");

        [Fact]
        public void BuildCluster_CompleteLabels_Works()
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Enable", "true" },
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.LoadBalancingPolicy", "LeastRequests" },
                { "YARP.Backend.SessionAffinity.Enabled", "true" },
                { "YARP.Backend.SessionAffinity.Policy", "Cookie" },
                { "YARP.Backend.SessionAffinity.FailurePolicy", "Return503Error" },
                { "YARP.Backend.SessionAffinity.AffinityKeyName", "Key1" },
                { "YARP.Backend.SessionAffinity.Cookie.Domain", "localhost" },
                { "YARP.Backend.SessionAffinity.Cookie.Expiration", "03:00:00" },
                { "YARP.Backend.SessionAffinity.Cookie.HttpOnly", "true" },
                { "YARP.Backend.SessionAffinity.Cookie.IsEssential", "true" },
                { "YARP.Backend.SessionAffinity.Cookie.MaxAge", "1.00:00:00" },
                { "YARP.Backend.SessionAffinity.Cookie.Path", "mypath" },
                { "YARP.Backend.SessionAffinity.Cookie.SameSite", "Strict" },
                { "YARP.Backend.SessionAffinity.Cookie.SecurePolicy", "SameAsRequest" },
                { "YARP.Backend.HttpRequest.ActivityTimeout", "00:00:17" },
                { "YARP.Backend.HttpRequest.AllowResponseBuffering", "true" },
                { "YARP.Backend.HttpRequest.Version", "1.1" },
#if NET
                { "YARP.Backend.HttpRequest.VersionPolicy", "RequestVersionExact" },
#endif
                { "YARP.Backend.HealthCheck.Active.Enabled", "true" },
                { "YARP.Backend.HealthCheck.Active.Interval", "00:00:05" },
                { "YARP.Backend.HealthCheck.Active.Timeout", "00:00:06" },
                { "YARP.Backend.HealthCheck.Active.Policy", "MyActiveHealthPolicy" },
                { "YARP.Backend.HealthCheck.Active.Path", "/api/health" },
                { "YARP.Backend.HealthCheck.Passive.Enabled", "true" },
                { "YARP.Backend.HealthCheck.Passive.Policy", "MyPassiveHealthPolicy" },
                { "YARP.Backend.HealthCheck.Passive.ReactivationPeriod", "00:00:07" },
                { "YARP.Backend.Metadata.Foo", "Bar" },

                { "YARP.Backend.HttpClient.DangerousAcceptAnyServerCertificate", "true" },
                { "YARP.Backend.HttpClient.MaxConnectionsPerServer", "1000" },
                { "YARP.Backend.HttpClient.SslProtocols", "Tls12" },
                { "YARP.Backend.HttpClient.ActivityContextHeaders", "BaggageAndCorrelationContext" },
#if NET
                { "YARP.Backend.HttpClient.EnableMultipleHttp2Connections", "false" },
                { "YARP.Backend.HttpClient.RequestHeaderEncoding", "utf-8" },
#endif
                { "YARP.Backend.HttpClient.WebProxy.Address", "https://10.20.30.40" },
                { "YARP.Backend.HttpClient.WebProxy.BypassOnLocal", "true" },
                { "YARP.Backend.HttpClient.WebProxy.UseDefaultCredentials", "false" },
            };

            var cluster = LabelsParser.BuildCluster(_testServiceName, labels, null);

            var expectedCluster = new ClusterConfig
            {
                ClusterId = "MyCoolClusterId",
                LoadBalancingPolicy = LoadBalancingPolicies.LeastRequests,
                SessionAffinity = new SessionAffinityConfig
                {
                    Enabled = true,
                    Policy = SessionAffinityConstants.Policies.Cookie,
                    FailurePolicy = SessionAffinityConstants.FailurePolicies.Return503Error,
                    AffinityKeyName = "Key1",
                    Cookie = new SessionAffinityCookieConfig
                    {
                        Domain = "localhost",
                        Expiration = TimeSpan.FromHours(3),
                        HttpOnly = true,
                        IsEssential = true,
                        MaxAge = TimeSpan.FromDays(1),
                        Path = "mypath",
                        SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                        SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                    }
                },
                HttpRequest = new ForwarderRequestConfig
                {
                    ActivityTimeout = TimeSpan.FromSeconds(17),
                    Version = new Version(1, 1),
                    AllowResponseBuffering = true,
#if NET
                    VersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact
#endif
                },
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Interval = TimeSpan.FromSeconds(5),
                        Timeout = TimeSpan.FromSeconds(6),
                        Path = "/api/health",
                        Policy = "MyActiveHealthPolicy"
                    },
                    Passive = new PassiveHealthCheckConfig
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
                HttpClient = new HttpClientConfig
                {
                    ActivityContextHeaders = ActivityContextHeaders.BaggageAndCorrelationContext,
                    DangerousAcceptAnyServerCertificate = true,
#if NET
                    EnableMultipleHttp2Connections = false,
                    RequestHeaderEncoding = "utf-8",
#endif
                    MaxConnectionsPerServer = 1000,
                    SslProtocols = SslProtocols.Tls12,
                    WebProxy = new WebProxyConfig
                    {
                        Address = new Uri("https://10.20.30.40"),
                        BypassOnLocal = true,
                        UseDefaultCredentials = false,
                    }
                }
            };
            cluster.Should().BeEquivalentTo(expectedCluster);
        }

        [Fact]
        public void BuildCluster_IncompleteLabels_UsesDefaultValues()
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.SessionAffinity.AffinityKeyName", "Key1" }
            };

            var cluster = LabelsParser.BuildCluster(_testServiceName, labels, null);

            var expectedCluster = new ClusterConfig
            {
                ClusterId = "MyCoolClusterId",
                SessionAffinity = new SessionAffinityConfig
                {
                    AffinityKeyName = "Key1",
                    Cookie = new SessionAffinityCookieConfig()
                },
                HttpRequest = new ForwarderRequestConfig(),
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig(),
                    Passive = new PassiveHealthCheckConfig()
                },
                Metadata = new Dictionary<string, string>(),
                HttpClient = new HttpClientConfig
                {
                    WebProxy = new WebProxyConfig
                    {
                    }
                }
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
        [InlineData(null, null)]
        [InlineData("", null)]
        public void BuildCluster_HealthCheckOptions_Enabled_Valid(string label, bool? expected)
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.HealthCheck.Active.Enabled", label },
            };

            var cluster = LabelsParser.BuildCluster(_testServiceName, labels, null);

            cluster.HealthCheck.Active.Enabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("notbool")]
        [InlineData(" ")]
        public void BuildCluster_HealthCheckOptions_Enabled_Invalid(string label)
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Backend.HealthCheck.Active.Enabled", label },
            };

            Action action = () => LabelsParser.BuildCluster(_testServiceName, labels, null);

            action.Should().Throw<ConfigException>();
        }

        [Fact]
        public void BuildCluster_MissingBackendId_UsesServiceName()
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.Quota.Burst", "2.3" },
                { "YARP.Backend.Partitioning.Count", "5" },
                { "YARP.Backend.Partitioning.KeyExtractor", "Header('x-ms-organization-id')" },
                { "YARP.Backend.Partitioning.Algorithm", "SHA256" },
                { "YARP.Backend.HealthCheck.Active.Interval", "00:00:5" },
            };

            var cluster = LabelsParser.BuildCluster(_testServiceName, labels, null);

            cluster.ClusterId.Should().Be(_testServiceName.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void BuildCluster_NullTimespan(string value)
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.HealthCheck.Active.Interval", value },
            };

            var cluster = LabelsParser.BuildCluster(_testServiceName, labels, null);

            cluster.HealthCheck.Active.Interval.Should().BeNull();
        }

        [Theory]
        [InlineData("YARP.Backend.HealthCheck.Active.Interval", "1S")]
        [InlineData("YARP.Backend.HealthCheck.Active.Timeout", "foobar")]
        public void BuildCluster_InvalidValues_Throws(string key, string invalidValue)
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { key, invalidValue },
            };

            Func<ClusterConfig> func = () => LabelsParser.BuildCluster(_testServiceName, labels, null);

            func.Should().Throw<ConfigException>().WithMessage($"Could not convert label {key}='{invalidValue}' *");
        }

        [Fact]
        public void BuildRoutes_SingleRoute_Works()
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Order", "2" },
                { "YARP.Routes.MyRoute.MatchHeaders.[0].Mode", "ExactHeader" },
                { "YARP.Routes.MyRoute.MatchHeaders.[0].Name", "x-company-key" },
                { "YARP.Routes.MyRoute.MatchHeaders.[0].Values", "contoso" },
                { "YARP.Routes.MyRoute.MatchHeaders.[0].IsCaseSensitive", "true" },
                { "YARP.Routes.MyRoute.MatchHeaders.[1].Mode", "ExactHeader" },
                { "YARP.Routes.MyRoute.MatchHeaders.[1].Name", "x-environment" },
                { "YARP.Routes.MyRoute.MatchHeaders.[1].Values", "dev, uat" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
                { "YARP.Routes.MyRoute.Transforms.[0].ResponseHeader", "X-Foo" },
                { "YARP.Routes.MyRoute.Transforms.[0].Append", "Bar" },
                { "YARP.Routes.MyRoute.Transforms.[0].When", "Always" },
                { "YARP.Routes.MyRoute.Transforms.[1].ResponseHeader", "X-Ping" },
                { "YARP.Routes.MyRoute.Transforms.[1].Append", "Pong" },
                { "YARP.Routes.MyRoute.Transforms.[1].When", "Success" },
            };

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match = new RouteMatch
                    {
                        Hosts = new[] { "example.com" },
                        Headers = new List<RouteHeader>
                        {
                            new RouteHeader()
                            {
                                Mode = HeaderMatchMode.ExactHeader,
                                Name = "x-company-key",
                                Values = new string[]{"contoso"},
                                IsCaseSensitive = true
                            },
                            new RouteHeader()
                            {
                                Mode = HeaderMatchMode.ExactHeader,
                                Name = "x-environment",
                                Values = new string[]{"dev", "uat"},
                                IsCaseSensitive = false
                            }
                        }
                    },
                    Order = 2,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>
                    {
                        { "Foo", "Bar" },
                    },
                    Transforms = new List<IReadOnlyDictionary<string, string>>
                    {
                        new Dictionary<string, string>
                        {
                            {"ResponseHeader", "X-Foo"},
                            {"Append", "Bar"},
                            {"When", "Always"}
                        },
                        new Dictionary<string, string>
                        {
                            {"ResponseHeader", "X-Ping"},
                            {"Append", "Pong"},
                            {"When", "Success"}
                        }
                    }
                },
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Fact]
        public void BuildRoutes_IncompleteRoute_UsesDefaults()
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
            };

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match = new RouteMatch
                    {
                        Hosts = new[] { "example.com" },
                    },
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
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "'this invalid thing" },
            };

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match = new RouteMatch
                    {
                        Hosts = new[] { "'this invalid thing" },
                    },
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
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Order", "2" },
            };

            if (scenario == 1)
            {
                labels.Add("YARP.Backend.BackendId", string.Empty);
            }

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = $"{Uri.EscapeDataString(_testServiceName.ToString())}:MyRoute",
                    Match = new RouteMatch
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
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Routes.MyRoute.Path", "/{**catchall}" },
            };

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = $"{Uri.EscapeDataString(_testServiceName.ToString())}:MyRoute",
                    Match = new RouteMatch
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
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Order", "this is no number" },
            };

            Func<List<RouteConfig>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

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
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { $"YARP.Routes.{routeName}.Hosts", "example.com" },
                { $"YARP.Routes.{routeName}.Order", "2" },
            };

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = $"MyCoolClusterId:{routeName}",
                    Match = new RouteMatch
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
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
            };
            labels[invalidKey] = value;

            Func<List<RouteConfig>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            func.Should()
                .Throw<ConfigException>()
                .WithMessage($"Invalid route name '*', should only contain alphanumerical characters, underscores or hyphens.");
        }

        [Theory]
        [InlineData("YARP.Routes.MyRoute.Transforms. .ResponseHeader", "Blank transform index")]
        [InlineData("YARP.Routes.MyRoute.Transforms.string.ResponseHeader", "string header name not accepted.. just [num]")]
        [InlineData("YARP.Routes.MyRoute.Transforms.1.Response", "needs square brackets")]
        public void BuildRoutes_InvalidTransformIndex_Throws(string invalidKey, string value)
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
            };
            labels[invalidKey] = value;

            Func<List<RouteConfig>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            func.Should()
                .Throw<ConfigException>()
                .WithMessage($"Invalid transform index '*', should be transform index wrapped in square brackets.");
        }

        [Theory]
        [InlineData("YARP.Routes.MyRoute.MatchHeaders. .Name", "x-header-name")]
        [InlineData("YARP.Routes.MyRoute.MatchHeaders.string.Name", "x-header-name")]
        [InlineData("YARP.Routes.MyRoute.MatchHeaders.1.Name", "x-header-name")]
        public void BuildRoutes_InvalidHeaderMatchIndex_Throws(string invalidKey, string value)
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
            Func<List<RouteConfig>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            func.Should()
                .Throw<ConfigException>()
                .WithMessage($"Invalid header matching index '*', should only contain alphanumerical characters, underscores or hyphens.");
        }

        [Theory]
        [InlineData("YARP.Routes.MyRoute.MatchHeaders.[0].UnknownProperty", "some value")]
        public void BuildRoutes_InvalidHeaderMatchProperty_Throws(string invalidKey, string value)
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
            Func<List<RouteConfig>> func = () => LabelsParser.BuildRoutes(_testServiceName, labels);

            // Assert
            func.Should()
                .Throw<ConfigException>()
                .WithMessage($"Invalid header matching property '*', only valid values are Name, Values, IsCaseSensitive and Mode.");
        }

        [Theory]
        [InlineData("YARP.Routes.MyRoute0.MatchHeaders.[0].Values", "apples, oranges, grapes", new string[] { "apples", "oranges", "grapes" })]
        [InlineData("YARP.Routes.MyRoute0.MatchHeaders.[0].Values", "apples,,oranges,grapes", new string[] { "apples", "", "oranges", "grapes" })]
        public void BuildRoutes_MatchHeadersWithCSVs_Works(string invalidKey, string value, string[] expected)
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute0.Hosts", "example0.com" },
                { "YARP.Routes.MyRoute0.Metadata.Foo", "bar" },
                { "YARP.Routes.MyRoute0.MatchHeaders.[0].Name", "x-test-header" },
                { "YARP.Routes.MyRoute0.MatchHeaders.[0].Mode", "ExactHeader" },
            };
            labels[invalidKey] = value;

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = $"MyCoolClusterId:MyRoute0",
                    Match = new RouteMatch
                    {
                        Hosts = new[] { "example0.com" },
                        Headers = new List<RouteHeader>() {
                            new RouteHeader(){Name = "x-test-header", Mode = HeaderMatchMode.ExactHeader, Values = expected},
                        }
                    },
                    Metadata = new Dictionary<string, string>(){
                        { "Foo", "bar"}
                    },
                    ClusterId = "MyCoolClusterId",
                }
            };
            routes.Should().BeEquivalentTo(expectedRoutes);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("NotEven.TheNamespace", "some value")]
        [InlineData("YARP.", "some value")]
        [InlineData("Routes.", "some value")]
        [InlineData("YARP.Routes.", "some value")]
        [InlineData("YARP.Routes.MyRoute.MatchHeaders", "some value")]
        [InlineData("YARP.Routes.MyRoute.MatchHeaders.", "some value")]
        [InlineData("YARP.Routes.MyRoute...MatchHeaders", "some value")]
        [InlineData("YARP.Routes.MyRoute.Transforms", "some value")]
        [InlineData("YARP.Routes.MyRoute.Transforms.", "some value")]
        [InlineData("YARP.Routes.MyRoute...Transforms", "some value")]
        [InlineData("YARP.Routes.MyRoute.Transform.", "some value")]
        [InlineData("YARP.Routes", "some value")]
        [InlineData("YARP..Routes.", "some value")]
        [InlineData("YARP.....Routes.", "some value")]
        public void BuildRoutes_InvalidLabelKeys_IgnoresAndDoesNotThrow(string invalidKey, string value)
        {
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Backend.BackendId", "MyCoolClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Order", "2" },
                { "YARP.Routes.MyRoute.Metadata.Foo", "Bar" },
            };
            labels[invalidKey] = value;

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match = new RouteMatch
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

            var routes = LabelsParser.BuildRoutes(_testServiceName, labels);

            var expectedRoutes = new List<RouteConfig>
            {
                new RouteConfig
                {
                    RouteId = "MyCoolClusterId:MyRoute",
                    Match = new RouteMatch
                    {
                        Hosts = new[] { "example.com" },
                        Path = "v2/{**rest}",
                    },
                    Order = 1,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string> { { "Foo", "Bar" } },
                },
                new RouteConfig
                {
                    RouteId = "MyCoolClusterId:CoolRoute",
                    Match = new RouteMatch
                    {
                        Hosts = new[] { "example.net" },
                    },
                    Order = 2,
                    ClusterId = "MyCoolClusterId",
                    Metadata = new Dictionary<string, string>(),
                },
                new RouteConfig
                {
                    RouteId = "MyCoolClusterId:EvenCoolerRoute",
                    Match = new RouteMatch
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
