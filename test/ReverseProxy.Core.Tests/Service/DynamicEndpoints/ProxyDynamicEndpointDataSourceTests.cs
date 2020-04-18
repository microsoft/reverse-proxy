// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Tests
{
    public class ProxyDynamicEndpointDataSourceTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new ProxyDynamicEndpointDataSource();
        }

        [Fact]
        public void Endpoints_StartsEmpty()
        {
            // Arrange
            var dataSource = new ProxyDynamicEndpointDataSource();

            // Act
            var endpoints = dataSource.Endpoints;

            // Assert
            Assert.Empty(endpoints);
        }

        [Fact]
        public void GetChangeToken_InitialValue()
        {
            // Arrange
            var dataSource = new ProxyDynamicEndpointDataSource();

            // Act
            var changeToken = dataSource.GetChangeToken();

            // Assert
            Assert.NotNull(changeToken);
            Assert.True(changeToken.ActiveChangeCallbacks);
            Assert.False(changeToken.HasChanged);
        }

        [Fact]
        public void GetChangeToken_SignalsChange()
        {
            // Arrange
            var dataSource = new ProxyDynamicEndpointDataSource();
            var newEndpoints1 = new List<AspNetCore.Http.Endpoint>();
            var newEndpoints2 = new List<AspNetCore.Http.Endpoint>();
            var signaled1 = 0;
            var signaled2 = 0;
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
            Assert.Equal(0, signaled1);
            dataSource.Update(newEndpoints1);
            Assert.Equal(1, signaled1);

            var changeToken2 = dataSource.GetChangeToken();
            changeToken2.RegisterChangeCallback(
                _ =>
                {
                    signaled2 = 1;
                    readEndpoints2 = dataSource.Endpoints;
                }, null);

            // updating again should only signal the new change token
            Assert.Equal(0, signaled2);
            dataSource.Update(newEndpoints2);
            Assert.Equal(1, signaled1);
            Assert.Equal(1, signaled2);

            Assert.Equal(newEndpoints1, readEndpoints1);
            Assert.Equal(newEndpoints2, readEndpoints2);
        }
    }
}
