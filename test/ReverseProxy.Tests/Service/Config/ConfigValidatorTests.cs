// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Tests
{
    public class ConfigValidatorTests
    {
        private IServiceProvider CreateServices(Action<IServiceCollection> configure = null)
        {
            var services = new ServiceCollection();
            services.AddReverseProxy();
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
    }
}
