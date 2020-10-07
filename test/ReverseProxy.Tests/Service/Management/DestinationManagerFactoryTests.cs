// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Service.RuntimeModel;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Management.Tests
{
    public class DestinationManagerFactoryTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new DestinationManagerFactory(null);
        }

        [Fact]
        public void CreateEndpointManager_CreatesNewInstances()
        {
            var changeListeners = new[] { new Mock<IDestinationChangeListener>().Object, new Mock<IDestinationChangeListener>().Object };
            // Arrange
            var factory = new DestinationManagerFactory(changeListeners);

            // Act
            var manager1 = factory.CreateDestinationManager();
            var manager2 = factory.CreateDestinationManager();

            // Assert
            Assert.NotNull(manager1);
            Assert.NotNull(manager2);
            Assert.NotSame(manager2, manager1);

            Assert.True(false, "Test destination change listeners.");
        }
    }
}
