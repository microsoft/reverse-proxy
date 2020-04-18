// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Management.Tests
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
            Assert.NotNull(manager1);
            Assert.NotNull(manager2);
            Assert.NotEqual(manager2, manager1);
        }
    }
}
