// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            var options = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                DangerousAcceptAnyServerCertificate = true,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 10
            };

            var clone = options.DeepClone();

            Assert.NotSame(options, clone);
            Assert.Equal(options.SslProtocols, clone.SslProtocols);
            Assert.Equal(options.DangerousAcceptAnyServerCertificate, clone.DangerousAcceptAnyServerCertificate);
            Assert.Same(options.ClientCertificate, clone.ClientCertificate);
            Assert.Equal(options.MaxConnectionsPerServer, clone.MaxConnectionsPerServer);
        }


        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20
            };

            var options2 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20
            };

            var equals = ProxyHttpClientOptions.Equals(options1, options2);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            var options1 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20
            };

            var options2 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls12,
                DangerousAcceptAnyServerCertificate = true,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20
            };

            var equals = ProxyHttpClientOptions.Equals(options1, options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            var options2 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls12,
                DangerousAcceptAnyServerCertificate = true,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20
            };

            var equals = ProxyHttpClientOptions.Equals(null, options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20
            };

            var equals = ProxyHttpClientOptions.Equals(options1, null);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            var equals = ProxyHttpClientOptions.Equals(null, null);

            Assert.True(equals);
        }
    }
}
