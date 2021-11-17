// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Health.Tests;

public class DefaultProbingRequestFactoryTests
{
    [Theory]
    [InlineData("https://localhost:10000/", null, null, "https://localhost:10000/")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/", null, "https://localhost:20000/")]
    [InlineData("https://localhost:10000/", null, "/api/health/", "https://localhost:10000/api/health/")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/", "/api/health/", "https://localhost:20000/api/health/")]
    [InlineData("https://localhost:10000/api", "https://localhost:20000/", "/health/", "https://localhost:20000/health/")]
    [InlineData("https://localhost:10000/", "https://localhost:20000/api", "/health/", "https://localhost:20000/api/health/")]
    public void CreateRequest_HealthEndpointIsNotDefined_UseDestinationAddress(string address, string health, string healthPath, string expectedRequestUri)
    {
        var clusterModel = GetClusterConfig("cluster0",
            new ActiveHealthCheckConfig()
            {
                Enabled = true,
                Policy = "policy",
                Path = healthPath,
            }, HttpVersion.Version20);
        var destinationModel = new DestinationModel(new DestinationConfig { Address = address, Health = health });
        var factory = new DefaultProbingRequestFactory();

        var request = factory.CreateRequest(clusterModel, destinationModel);

        Assert.Equal(expectedRequestUri, request.RequestUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData(null)]
    public void CreateRequest_RequestVersionProperties(string versionString)
    {
        var version = versionString != null ? Version.Parse(versionString) : null;
        var clusterModel = GetClusterConfig("cluster0",
            new ActiveHealthCheckConfig()
            {
                Enabled = true,
                Policy = "policy",
            },
            version
#if NET
            , HttpVersionPolicy.RequestVersionExact
#endif
            );
        var destinationModel = new DestinationModel(new DestinationConfig { Address = "https://localhost:10000/" });
        var factory = new DefaultProbingRequestFactory();

        var request = factory.CreateRequest(clusterModel, destinationModel);

        Assert.Equal(version ?? HttpVersion.Version20, request.Version);
#if NET
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, request.VersionPolicy);
#endif
    }

    private ClusterModel GetClusterConfig(string id, ActiveHealthCheckConfig healthCheckOptions, Version version
#if NET
        , HttpVersionPolicy versionPolicy = HttpVersionPolicy.RequestVersionExact
#endif
        )
    {
        return new ClusterModel(
            new ClusterConfig
            {
                ClusterId = id,
                HealthCheck = new HealthCheckConfig()
                {
                    Active = healthCheckOptions,
                },
                HttpRequest = new ForwarderRequestConfig
                {
                    ActivityTimeout = TimeSpan.FromSeconds(60),
                    Version = version,
#if NET
                    VersionPolicy = versionPolicy,
#endif
                }
            },
            new HttpMessageInvoker(new HttpClientHandler()));
    }
}
