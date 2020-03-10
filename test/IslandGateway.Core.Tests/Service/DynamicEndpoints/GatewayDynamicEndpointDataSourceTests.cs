// <copyright file="GatewayDynamicEndpointDataSourceTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using AspNetCore = Microsoft.AspNetCore;

namespace IslandGateway.Core.Service.Tests
{
    public class GatewayDynamicEndpointDataSourceTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new GatewayDynamicEndpointDataSource();
        }

        [Fact]
        public void Endpoints_StartsEmpty()
        {
            // Arrange
            var dataSource = new GatewayDynamicEndpointDataSource();

            // Act
            var endpoints = dataSource.Endpoints;

            // Assert
            endpoints.Should().BeEmpty();
        }

        [Fact]
        public void GetChangeToken_InitialValue()
        {
            // Arrange
            var dataSource = new GatewayDynamicEndpointDataSource();

            // Act
            var changeToken = dataSource.GetChangeToken();

            // Assert
            changeToken.Should().NotBeNull();
            changeToken.ActiveChangeCallbacks.Should().BeTrue();
            changeToken.HasChanged.Should().BeFalse();
        }

        [Fact]
        public void GetChangeToken_SignalsChange()
        {
            // Arrange
            var dataSource = new GatewayDynamicEndpointDataSource();
            var newEndpoints1 = new List<AspNetCore.Http.Endpoint>();
            var newEndpoints2 = new List<AspNetCore.Http.Endpoint>();
            int signaled1 = 0;
            int signaled2 = 0;
            IReadOnlyList<AspNetCore.Http.Endpoint> readEndpoints1 = null;
            IReadOnlyList<AspNetCore.Http.Endpoint> readEndpoints2 = null;

            // Act & Assert
            var changeToken1 = dataSource.GetChangeToken();
            changeToken1.RegisterChangeCallback(
                _ =>
                {
                    signaled1 = 1;
                    readEndpoints1 = dataSource.Endpoints;
                }, null);

            // updating should signal the current change token
            signaled1.Should().Be(0);
            dataSource.Update(newEndpoints1);
            signaled1.Should().Be(1);

            var changeToken2 = dataSource.GetChangeToken();
            changeToken2.RegisterChangeCallback(
                _ =>
                {
                    signaled2 = 1;
                    readEndpoints2 = dataSource.Endpoints;
                }, null);

            // updating again should only signal the new change token
            signaled2.Should().Be(0);
            dataSource.Update(newEndpoints2);
            signaled1.Should().Be(1);
            signaled2.Should().Be(1);

            readEndpoints1.Should().BeSameAs(newEndpoints1);
            readEndpoints2.Should().BeSameAs(newEndpoints2);
        }
    }
}
