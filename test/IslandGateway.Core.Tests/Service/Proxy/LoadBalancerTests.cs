// <copyright file="LoadBalancerTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using FluentAssertions;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.RuntimeModel;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Proxy.Tests
{
    public class LoadBalancerTests : TestAutoMockBase
    {
        [Fact]
        public void PickEndpoint_FirstWithoutEndpoints_Works()
        {
            // Arrange
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new EndpointInfo[0];
            var options = new BackendConfig.BackendLoadBalancingOptions(BackendConfig.BackendLoadBalancingOptions.LoadBalancingMode.First);

            // Act
            var result = loadBalancer.PickEndpoint(endpoints, endpoints, in options);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void PickEndpoint_FirstWithEndpoints_Works()
        {
            // Arrange
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
                new EndpointInfo("ep2"),
            };
            var options = new BackendConfig.BackendLoadBalancingOptions(BackendConfig.BackendLoadBalancingOptions.LoadBalancingMode.First);

            // Act
            var result = loadBalancer.PickEndpoint(endpoints, endpoints, in options);

            // Assert
            result.Should().BeSameAs(endpoints[0]);
        }

        [Fact]
        public void PickEndpoint_UnsupportedMode_Throws()
        {
            // Arrange
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new EndpointInfo[0];
            var options = new BackendConfig.BackendLoadBalancingOptions((BackendConfig.BackendLoadBalancingOptions.LoadBalancingMode)(-1));

            // Act
            Action action = () => loadBalancer.PickEndpoint(endpoints, endpoints, in options);

            // Assert
            action.Should().ThrowExactly<GatewayException>()
                .Which.Message.Should().Be("Load balancing mode '-1' is not supported.");
        }
    }
}
