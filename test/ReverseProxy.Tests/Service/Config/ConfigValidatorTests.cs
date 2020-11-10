// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service.HealthChecks;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Tests
{
    public class ConfigValidatorTests
    {
        private IServiceProvider CreateServices(Action<IServiceCollection> configure = null)
        {
            var services = new ServiceCollection();
            services.AddReverseProxy();
            var passivePolicy = new Mock<IPassiveHealthCheckPolicy>();
            passivePolicy.SetupGet(p => p.Name).Returns("passive0");
            services.AddSingleton(passivePolicy.Object);
            services.AddOptions();
            services.AddLogging();
            services.AddRouting();
            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Constructor_Works()
        {
            var services = CreateServices();
            services.GetRequiredService<IConfigValidator>();
        }

        [Theory]
        [InlineData("example.com", "/a/", null)]
        [InlineData("example.com", "/a/**", null)]
        [InlineData("example.com", "/a/**", "GET")]
        [InlineData(null, "/a/", null)]
        [InlineData(null, "/a/**", "GET")]
        [InlineData("example.com", null, "get")]
        [InlineData("example.com", null, "gEt,put")]
        [InlineData("example.com", null, "gEt,put,POST,traCE,PATCH,DELETE,Head")]
        [InlineData("example.com,example2.com", null, "get")]
        [InlineData("*.example.com", null, null)]
        [InlineData("a-b.example.com", null, null)]
        [InlineData("a-b.b-c.example.com", null, null)]
        public async Task Accepts_ValidRules(string host, string path, string methods)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Hosts = host?.Split(",") ?? Array.Empty<string>(),
                    Path = path,
                    Methods = methods?.Split(","),
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.Empty(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Rejects_MissingRouteId(string routeId)
        {
            var route = new ProxyRoute { RouteId = routeId };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.Equals("Missing Route Id."));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("example.com,")]
        public async Task Rejects_MissingHostAndPath(string host)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match =
                {
                    Hosts = host?.Split(",")
                },
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.Equals("Route 'route1' requires Hosts or Path specified. Set the Path to '/{**catchall}' to match all requests."));
        }

        [Theory]
        [InlineData(".example.com")]
        [InlineData("example*.com")]
        [InlineData("example.*.com")]
        [InlineData("example.*a.com")]
        [InlineData("*example.com")]
        [InlineData("-example.com")]
        [InlineData("example-.com")]
        [InlineData("-example-.com")]
        [InlineData("a.-example.com")]
        [InlineData("a.example-.com")]
        [InlineData("a.-example-.com")]
        [InlineData("example.com,example-.com")]
        public async Task Rejects_InvalidHost(string host)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Hosts = host.Split(","),
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.StartsWith("Invalid host name"));
        }

        [Theory]
        [InlineData("/{***a}")]
        [InlineData("/{")]
        [InlineData("/}")]
        [InlineData("/{ab/c}")]
        public async Task Rejects_InvalidPath(string path)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Path = path,
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.Equals($"Invalid path '{path}' for route 'route1'."));
        }

        [Theory]
        [InlineData("")]
        [InlineData("gett")]
        public async Task Rejects_InvalidMethod(string methods)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Methods = methods.Split(","),
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.Equals($"Unsupported HTTP method '{methods}' has been set for route 'route1'."));
        }

        [Theory]
        [InlineData("get,GET")]
        [InlineData("get,post,get")]
        public async Task Rejects_DuplicateMethod(string methods)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Methods = methods.Split(","),
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.StartsWith("Duplicate HTTP method"));
        }

        [Fact]
        public async Task Accepts_RouteHeader()
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Path = "/",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                        }
                    },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Accepts_RouteHeader_ExistsWithNoValue()
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Path = "/",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Mode = HeaderMatchMode.Exists
                        }
                    },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Rejects_NullRouteHeader()
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Path = "/",
                    Headers = new RouteHeader[] { null },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            var ex = Assert.Single(result);
            Assert.Contains("A null route header has been set for route", ex.Message);
        }

        [Theory]
        [InlineData("", "v1", HeaderMatchMode.ExactHeader, "A null or empty route header name has been set for route")]
        [InlineData("h1", null, HeaderMatchMode.ExactHeader, "No header values were set on route header")]
        [InlineData("h1", "v1", HeaderMatchMode.Exists, "Header values where set when using mode 'Exists'")]
        public async Task Rejects_InvalidRouteHeader(string name, string value, HeaderMatchMode mode, string error)
        {
            var routeHeader = new RouteHeader()
            {
                Name = name,
                Mode = mode,
            };
            if (value != null)
            {
                routeHeader.Values = new[] { value };
            }

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Path = "/",
                    Headers = new[] { routeHeader },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            var ex = Assert.Single(result);
            Assert.Contains(error, ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("defaulT")]
        public async Task Accepts_ReservedAuthorizationPolicy(string policy)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                AuthorizationPolicy = policy,
                Match =
                {
                    Hosts = new[] { "localhost" },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Accepts_CustomAuthorizationPolicy()
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                AuthorizationPolicy = "custom",
                Match =
                {
                    Hosts = new[] { "localhost" },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices(services =>
            {
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("custom", builder => builder.RequireAuthenticatedUser());
                });
            });
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Rejects_UnknownAuthorizationPolicy()
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                AuthorizationPolicy = "unknown",
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.Equals("Authorization policy 'unknown' not found for route 'route1'."));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("defaulT")]
        [InlineData("disAble")]
        public async Task Accepts_ReservedCorsPolicy(string policy)
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                CorsPolicy = policy,
                Match =
                {
                    Hosts = new[] { "localhost" },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Accepts_CustomCorsPolicy()
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                CorsPolicy = "custom",
                Match =
                {
                    Hosts = new[] { "localhost" },
                },
                ClusterId = "cluster1",
            };

            var services = CreateServices(services =>
            {
                services.AddCors(options =>
                {
                    options.AddPolicy("custom", new CorsPolicy());
                });
            });
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Rejects_UnknownCorsPolicy()
        {
            var route = new ProxyRoute
            {
                RouteId = "route1",
                CorsPolicy = "unknown",
                ClusterId = "cluster1",
            };

            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var result = await validator.ValidateRouteAsync(route);

            Assert.NotEmpty(result);
            Assert.Contains(result, err => err.Message.Equals("CORS policy 'unknown' not found for route 'route1'."));
        }

        [Fact]
        public async Task EmptyCluster_Works()
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Empty(errors);
        }

        [Fact]
        public async Task EnableSessionAffinity_Works()
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                SessionAffinity = new SessionAffinityOptions()
                {
                    Enabled = true
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Empty(errors);
        }

        [Fact]
        public async Task EnableSession_InvalidMode_Fails()
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                SessionAffinity = new SessionAffinityOptions()
                {
                    Enabled = true,
                    Mode = "Invalid"
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            var ex = Assert.Single(errors);
            Assert.Equal("No matching ISessionAffinityProvider found for the session affinity mode 'Invalid' set on the cluster 'cluster1'.", ex.Message);
        }

        [Fact]
        public async Task EnableSession_InvalidPolicy_Fails()
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                SessionAffinity = new SessionAffinityOptions()
                {
                    Enabled = true,
                    FailurePolicy = "Invalid"
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            var ex = Assert.Single(errors);
            Assert.Equal("No matching IAffinityFailurePolicy found for the affinity failure policy name 'Invalid' set on the cluster 'cluster1'.", ex.Message);
        }

        [Fact]
        public async Task Accepts_RequestVersion_Null()
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                HttpRequest = new ProxyHttpRequestOptions()
                {
                    Version = null,
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData(1,0)]
        [InlineData(1,1)]
        [InlineData(2,0)]
        public async Task Accepts_RequestVersion(int major, int minor)
        {
            var version = new Version(major, minor);
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                HttpRequest = new ProxyHttpRequestOptions()
                {
                    Version = version,
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData(1,9)]
        [InlineData(2,5)]
        [InlineData(3,0)]
        public async Task Rejects_RequestVersion(int major, int minor)
        {
            var version = new Version(major, minor);
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                HttpRequest = new ProxyHttpRequestOptions()
                {
                    Version = version,
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Equal(1, errors.Count);
            Assert.Equal($"Outgoing request version '{cluster.HttpRequest.Version}' is not any of supported HTTP versions (1.0, 1.1 and 2).", errors[0].Message);
            Assert.IsType<ArgumentException>(errors[0]);
        }

        [Theory]
        [InlineData(null, null, null, "ConsecutiveFailures")]
        [InlineData(25, null, null, "ConsecutiveFailures")]
        [InlineData(25, 10, null, "ConsecutiveFailures")]
        [InlineData(25, 10, "/api/health", "ConsecutiveFailures")]
        public async Task EnableActiveHealthCheck_Works(int? interval, int? timeout, string path, string policy)
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                HealthCheck = new HealthCheckOptions
                {
                    Active = new ActiveHealthCheckOptions {
                        Enabled = true,
                        Interval = interval != null ? TimeSpan.FromSeconds(interval.Value) : (TimeSpan?)null,
                        Path = path,
                        Policy = policy,
                        Timeout = timeout != null ? TimeSpan.FromSeconds(timeout.Value) : (TimeSpan?)null
                    }
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData(null, null, null, "Active health policy name is not set")]
        [InlineData(-1, null, "ConsecutiveFailures", "Destination probing interval")]
        [InlineData(null, -1, "ConsecutiveFailures", "Destination probing timeout")]
        public async Task EnableActiveHealthCheck_InvalidParameter_ErrorReturned(int? interval, int? timeout, string policy, string expectedError)
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                HealthCheck = new HealthCheckOptions
                {
                    Active = new ActiveHealthCheckOptions
                    {
                        Enabled = true,
                        Interval = interval != null ? TimeSpan.FromSeconds(interval.Value) : (TimeSpan?)null,
                        Policy = policy,
                        Timeout = timeout != null ? TimeSpan.FromSeconds(timeout.Value) : (TimeSpan?)null
                    }
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Equal(1, errors.Count);
            Assert.Contains(expectedError, errors[0].Message);
            Assert.IsType<ArgumentException>(errors[0]);
        }

        [Theory]
        [InlineData(null, "passive0")]
        [InlineData(25, "passive0")]
        public async Task EnablePassiveHealthCheck_Works(int? reactivationPeriod, string policy)
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                HealthCheck = new HealthCheckOptions
                {
                    Passive = new PassiveHealthCheckOptions
                    {
                        Enabled = true, Policy = policy, ReactivationPeriod = reactivationPeriod != null ? TimeSpan.FromSeconds(reactivationPeriod.Value) : (TimeSpan?)null
                    }
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData(null, null, "Passive health policy name is not set")]
        [InlineData(-1, "passive0", "Unhealthy destination reactivation period")]
        public async Task EnablePassiveHealthCheck_InvalidParameter_ErrorReturned(int? reactivationPeriod, string policy, string expectedError)
        {
            var services = CreateServices();
            var validator = services.GetRequiredService<IConfigValidator>();

            var cluster = new Cluster
            {
                Id = "cluster1",
                HealthCheck = new HealthCheckOptions
                {
                    Passive = new PassiveHealthCheckOptions
                    {
                        Enabled = true,
                        Policy = policy,
                        ReactivationPeriod = reactivationPeriod != null ? TimeSpan.FromSeconds(reactivationPeriod.Value) : (TimeSpan?)null
                    }
                }
            };

            var errors = await validator.ValidateClusterAsync(cluster);

            Assert.Equal(1, errors.Count);
            Assert.Contains(expectedError, errors[0].Message);
            Assert.IsType<ArgumentException>(errors[0]);
        }
    }
}
