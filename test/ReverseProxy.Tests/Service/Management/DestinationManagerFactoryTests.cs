// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Yarp.ReverseProxy.Service.Management.Tests
{
    public class DestinationManagerFactoryTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new DestinationManagerFactory();
        }

        [Fact]
        public void CreateEndpointManager_CreatesNewInstances()
        {
            var factory = new DestinationManagerFactory();

            var manager1 = factory.CreateDestinationManager();
            var manager2 = factory.CreateDestinationManager();

            Assert.NotNull(manager1);
            Assert.NotNull(manager2);
            Assert.NotSame(manager2, manager1);
        }
    }
}
