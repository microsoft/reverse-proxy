// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Forwarder;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class ConfigValidatorTests
{
    private IServiceProvider CreateServices(Action<IServiceCollection> configure = null)
    {
        var services = new ServiceCollection();
        services.AddReverseProxy();
        var passivePolicy = new Mock<IPassiveHealthCheckPolicy>();
        passivePolicy.SetupGet(p => p.Name).Returns("passive0");
        services.AddSingleton(passivePolicy.Object);
        var availableDestinationsPolicy = new Mock<IAvailableDestinationsPolicy>();
        availableDestinationsPolicy.SetupGet(p => p.Name).Returns("availableDestinations0");
        services.AddSingleton(availableDestinationsPolicy.Object);
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
    [InlineData("example.com:80", "/a/", null)]
    [InlineData("\u00FCnicode", "/a/", null)]
    [InlineData("\u00FCnicode:443", "/a/", null)]
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
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
        var route = new RouteConfig { RouteId = routeId };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Equals("Missing Route Id."));
    }

    [Fact]
    public async Task Rejects_MissingMatch()
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Equals("Route 'route1' did not set any match criteria, it requires Hosts or Path specified. Set the Path to '/{**catchall}' to match all requests."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xn--nicode-2ya")]
    [InlineData("Xn--nicode-2ya")]
    public async Task Rejects_InvalidHost(string host)
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Hosts = new[] { host }
            },
            ClusterId = "cluster1",
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Contains("host name"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData("", null)]
    [InlineData(",", null)]
    [InlineData("", "")]
    public async Task Rejects_MissingHostAndPath(string host, string path)
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch
            {
                Hosts = host?.Split(","),
                Path = path
            },
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Equals("Route 'route1' requires Hosts or Path specified. Set the Path to '/{**catchall}' to match all requests."));
    }

    [Theory]
    [InlineData("/{***a}")]
    [InlineData("/{")]
    [InlineData("/}")]
    [InlineData("/{ab/c}")]
    public async Task Rejects_InvalidPath(string path)
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch()
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
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
    public async Task Accepts_RouteQueryParameter()
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/",
                QueryParameters = new[]
                {
                    new RouteQueryParameter()
                    {
                        Name = "queryparam1",
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
    public async Task Accepts_RoutePathParameter()
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/people/{people_id}",
                PathParameters = new[]
                {
                    new RoutePathParameter()
                    {
                        Name = "people_id",
                        Values = new[] { "42" },
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
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
    public async Task Accepts_RouteQueryParameter_ExistsWithNoValue()
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/",
                QueryParameters = new[]
                {
                    new RouteQueryParameter()
                    {
                        Name = "queryparam1",
                        Mode = QueryParameterMatchMode.Exists
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
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

    [Fact]
    public async Task Rejects_NullRouteQueryParameter()
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/",
                QueryParameters = new RouteQueryParameter[] { null },
            },
            ClusterId = "cluster1",
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        var ex = Assert.Single(result);
        Assert.Contains("A null route query parameter has been set for route", ex.Message);
    }

    [Fact]
    public async Task Rejects_NullRoutePathParameter()
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/",
                PathParameters = new RoutePathParameter[] { null },
            },
            ClusterId = "cluster1",
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        var ex = Assert.Single(result);
        Assert.Contains("A null route path parameter has been set for route", ex.Message);
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
            Values = value == null ? null : new[] { value },
        };

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
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
    [InlineData("", "v1", QueryParameterMatchMode.Exact, "A null or empty route query parameter name has been set for route")]
    [InlineData("h1", null, QueryParameterMatchMode.Exact, "No query parameter values were set on route query parameter")]
    [InlineData("h1", "v1", QueryParameterMatchMode.Exists, "Query parameter values where set when using mode 'Exists'")]
    public async Task Rejects_InvalidRouteQueryParameter(string name, string value, QueryParameterMatchMode mode, string error)
    {
        var routeQueryParameter = new RouteQueryParameter()
        {
            Name = name,
            Mode = mode,
            Values = value == null ? null : new[] { value },
        };

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/",
                QueryParameters = new[] { routeQueryParameter },
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
    [InlineData("", new[] { "v1" }, "A null or empty route path parameter name has been set for route '")]
    [InlineData(null, new[] { "v1" }, "A null or empty route path parameter name has been set for route '")]
    [InlineData("p1", null, "No path parameter values was set on route path parameter '")]
    [InlineData("p1", new string[] { }, "No path parameter values was set on route path parameter '")]
    [InlineData("p1", new[] { "v1", null }, "At least one null or empty path parameter value was set on route path parameter '")]
    [InlineData("p1", new[] { "", "v2" }, "At least one null or empty path parameter value was set on route path parameter '")]
    public async Task Rejects_InvalidRoutePathParameter(string name, IReadOnlyList<string> values, string error)
    {
        var routePathParameter = new RoutePathParameter()
        {
            Name = name,
            Mode = PathParameterMatchMode.Exact,
            Values = values,
        };

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/{p1}",
                PathParameters = new[] { routePathParameter },
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
    [InlineData("/{something")]
    [InlineData("/{something}}")]
    public async Task Rejects_RoutePathParameter_InvalidPath(string path)
    {
        var routePathParameter = new RoutePathParameter()
        {
            Name = "something",
            Mode = PathParameterMatchMode.Exact,
            Values = new[] { "z" },
        };

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = path,
                PathParameters = new[] { routePathParameter },
            },
            ClusterId = "cluster1",
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        var ex = Assert.Single(result);
        Assert.Contains("Invalid path '", ex.Message);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/static/{id}")]
    [InlineData("/static/{id}/{more}")]
    public async Task Rejects_NonMatchingRoutePathParameter(string path)
    {
        var routePathParameter = new RoutePathParameter()
        {
            Name = "something",
            Mode = PathParameterMatchMode.Exact,
            Values = new[] { "irrelevant" },
        };

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = path,
                PathParameters = new[] { routePathParameter },
            },
            ClusterId = "cluster1",
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        var ex = Assert.Single(result);
        Assert.Contains("something", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("defaulT")]
    public async Task Accepts_ReservedAuthorizationPolicy(string policy)
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            AuthorizationPolicy = policy,
            Match = new RouteMatch
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            AuthorizationPolicy = "custom",
            Match = new RouteMatch
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            AuthorizationPolicy = "unknown",
            ClusterId = "cluster1",
            Match = new RouteMatch(),
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Equals("Authorization policy 'unknown' not found for route 'route1'."));
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Anonymous")]
    public async Task Rejects_ReservedAuthorizationPolicyIsUsed(string authorizationPolicy)
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            AuthorizationPolicy = authorizationPolicy,
            ClusterId = "cluster1",
            Match = new RouteMatch(),
        };

        var services = CreateServices(serviceCollection =>
        {
            serviceCollection.AddAuthorization(options =>
            {
                options.AddPolicy(authorizationPolicy, builder =>
                {
                    builder.RequireAuthenticatedUser();
                });
            });
        });
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Equals($"The application has registered an authorization policy named '{authorizationPolicy}' that conflicts with the reserved authorization policy name used on this route. The registered policy name needs to be changed for this route to function."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("defaulT")]
    [InlineData("disAble")]
    public async Task Accepts_ReservedCorsPolicy(string policy)
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            CorsPolicy = policy,
            Match = new RouteMatch
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            CorsPolicy = "custom",
            Match = new RouteMatch
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
        var route = new RouteConfig
        {
            RouteId = "route1",
            CorsPolicy = "unknown",
            ClusterId = "cluster1",
            Match = new RouteMatch(),
        };

        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Equals("CORS policy 'unknown' not found for route 'route1'."));
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Disable")]
    public async Task Rejects_ReservedCorsPolicyIsUsed(string corsPolicy)
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            CorsPolicy = corsPolicy,
            ClusterId = "cluster1",
            Match = new RouteMatch
            {
                Hosts = new[] { "localhost" },
            },
        };

        var services = CreateServices(serviceCollection =>
        {
            serviceCollection.AddCors(options =>
            {
                options.AddPolicy(corsPolicy, builder =>
                {

                });
            });
        });
        var validator = services.GetRequiredService<IConfigValidator>();

        var result = await validator.ValidateRouteAsync(route);

        Assert.NotEmpty(result);
        Assert.Contains(result, err => err.Message.Equals($"The application has registered a CORS policy named '{corsPolicy}' that conflicts with the reserved CORS policy name used on this route. The registered policy name needs to be changed for this route to function."));
    }

    [Fact]
    public async Task EmptyCluster_Works()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task LoadBalancingPolicy_KnownPolicy_Works()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task LoadBalancingPolicy_UnknownPolicy_Fails()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            LoadBalancingPolicy = "MyCustomPolicy"
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        var ex = Assert.Single(errors);
        Assert.Equal("No matching ILoadBalancingPolicy found for the load balancing policy 'MyCustomPolicy' set on the cluster 'cluster1'.", ex.Message);
    }

    [Fact]
    public async Task EnableSessionAffinity_Works()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                AffinityKeyName = "SomeKey"
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task EnableSessionAffinity_InvalidPolicy_Fails()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                FailurePolicy = "Invalid",
                AffinityKeyName = "SomeKey"
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        var ex = Assert.Single(errors);
        Assert.Equal("No matching IAffinityFailurePolicy found for the affinity failure policy name 'Invalid' set on the cluster 'cluster1'.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task EnableSessionAffinity_AffinityIsNotSet_Fails(string key)
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                AffinityKeyName = key
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        var ex = Assert.Single(errors);
        Assert.Equal("Affinity key name set on the cluster 'cluster1' must not be null.", ex.Message);
    }

    [Fact]
    public async Task Accepts_RequestVersion_Null()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HttpRequest = new ForwarderRequestConfig
            {
                Version = null,
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    public async Task Accepts_RequestVersion(int major, int minor)
    {
        var version = new Version(major, minor);
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HttpRequest = new ForwarderRequestConfig
            {
                Version = version,
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(2, 5)]
    [InlineData(3, 0)]
    public async Task Rejects_RequestVersion(int major, int minor)
    {
        var version = new Version(major, minor);
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HttpRequest = new ForwarderRequestConfig
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
    [InlineData(null, null, null, null)]
    [InlineData(null, null, null, "")]
    [InlineData(null, null, null, "ConsecutiveFailures")]
    [InlineData(25, null, null, "ConsecutiveFailures")]
    [InlineData(25, 10, null, "ConsecutiveFailures")]
    [InlineData(25, 10, "/api/health", "ConsecutiveFailures")]
    public async Task EnableActiveHealthCheck_Works(int? interval, int? timeout, string path, string policy)
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig
                {
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
    [InlineData(-1, null, "ConsecutiveFailures", "Destination probing interval")]
    [InlineData(null, -1, "ConsecutiveFailures", "Destination probing timeout")]
    [InlineData(null, null, "NonExistingPolicy", "No matching IActiveHealthCheckPolicy found for the active health check policy")]
    public async Task EnableActiveHealthCheck_InvalidParameter_ErrorReturned(int? interval, int? timeout, string policy, string expectedError)
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig
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
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData(null, "passive0")]
    [InlineData(25, "passive0")]
    public async Task EnablePassiveHealthCheck_Works(int? reactivationPeriod, string policy)
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                Passive = new PassiveHealthCheckConfig
                {
                    Enabled = true,
                    Policy = policy,
                    ReactivationPeriod = reactivationPeriod != null ? TimeSpan.FromSeconds(reactivationPeriod.Value) : (TimeSpan?)null
                }
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(-1, "passive0", "Unhealthy destination reactivation period")]
    [InlineData(1, "NonExistingPolicy", "No matching IPassiveHealthCheckPolicy found for the passive health check policy")]
    public async Task EnablePassiveHealthCheck_InvalidParameter_ErrorReturned(int? reactivationPeriod, string policy, string expectedError)
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                Passive = new PassiveHealthCheckConfig
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("availableDestinations0")]
    public async Task SetAvailableDestinationsPolicy_Works(string policy)
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                AvailableDestinationsPolicy = policy
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task SetAvailableDestinationsPolicy_Invalid()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();
        const string policy = "Unknown1";

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                AvailableDestinationsPolicy = policy
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        const string expectedError = "No matching IAvailableDestinationsPolicy found for the available destinations policy 'Unknown1' set on the cluster.";
        Assert.Equal(1, errors.Count);
        Assert.Contains(expectedError, errors[0].Message);
        Assert.IsType<ArgumentException>(errors[0]);
    }
#if NET
    [Fact]
    public async Task HttpClient_HeaderEncoding_Valid()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HttpClient = new HttpClientConfig
            {
                RequestHeaderEncoding = "utf-8"
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public async Task HttpClient_HeaderEncoding_Invalid()
    {
        var services = CreateServices();
        var validator = services.GetRequiredService<IConfigValidator>();

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            HttpClient = new HttpClientConfig
            {
                RequestHeaderEncoding = "base64"
            }
        };

        var errors = await validator.ValidateClusterAsync(cluster);

        Assert.Equal(1, errors.Count);
        Assert.Equal("Invalid header encoding 'base64'.", errors[0].Message);
    }
#endif
}
