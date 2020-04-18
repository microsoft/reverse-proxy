// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
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
            Assert.Null(result);
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
            Assert.Equal(endpoints[0], result);
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
            Assert.Throws<ReverseProxyException>(action);
        }
    }
}
