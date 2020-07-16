// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
{
    public class ProxyHttpClientFactoryTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            new ProxyHttpClientFactory();
        }

        [Fact]
        public void CreateClient_Works()
        {
            // Arrange
            var factory = new ProxyHttpClientFactory();

            // Act
            var actual1 = factory.CreateClient();
            var actual2 = factory.CreateClient();

            // Assert
            Assert.NotNull(actual1);
            Assert.NotNull(actual2);
            Assert.NotSame(actual2, actual1);
        }
    }
}
