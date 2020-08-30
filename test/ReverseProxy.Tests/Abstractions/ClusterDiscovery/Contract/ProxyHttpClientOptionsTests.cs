// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Security.Authentication;
using Microsoft.ReverseProxy.Utilities.Tests;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class ProxyHttpClientOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new ProxyHttpClientOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var options = new ProxyHttpClientOptions
            {
                SslProtocols = new List<SslProtocols> { SslProtocols.Tls12, SslProtocols.Tls13},
                ValidateRemoteCertificate = true,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 10
            };

            // Act
            var clone = options.DeepClone();

            // Assert
            Assert.NotSame(options, clone);
            Assert.NotSame(options.SslProtocols, clone.SslProtocols);
            Assert.Equal(options.SslProtocols.Count, clone.SslProtocols.Count);
            Assert.Equal(options.SslProtocols[0], clone.SslProtocols[0]);
            Assert.Equal(options.SslProtocols[1], clone.SslProtocols[1]);
            Assert.Equal(options.ValidateRemoteCertificate, clone.ValidateRemoteCertificate);
            Assert.Same(options.ClientCertificate, clone.ClientCertificate);
            Assert.Equal(options.MaxConnectionsPerServer, clone.MaxConnectionsPerServer);
        }
    }
}
