// <copyright file="EndpointManagerFactoryTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Service.Management.Tests
{
    public class EndpointManagerFactoryTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new EndpointManagerFactory();
        }

        [Fact]
        public void CreateEndpointManager_CreatesNewInstances()
        {
            // Arrange
            var factory = new EndpointManagerFactory();

            // Act
            var manager1 = factory.CreateEndpointManager();
            var manager2 = factory.CreateEndpointManager();

            // Assert
            manager1.Should().NotBeNull();
            manager2.Should().NotBeNull();
            manager1.Should().NotBeSameAs(manager2);
        }
    }
}
