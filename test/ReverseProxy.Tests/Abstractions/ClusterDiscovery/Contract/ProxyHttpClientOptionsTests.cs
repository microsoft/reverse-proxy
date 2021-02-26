// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Authentication;
using System.Text;
using Microsoft.ReverseProxy.Utilities.Tests;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class ProxyHttpClientOptionsTests
    {
        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20,
#if NET
                RequestHeaderEncoding = Encoding.UTF8
#endif
            };

            var options2 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20,
#if NET
                RequestHeaderEncoding = Encoding.UTF8
#endif
            };

            var equals = options1.Equals(options2);

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
                MaxConnectionsPerServer = 20,
#if NET
                RequestHeaderEncoding = Encoding.UTF8
#endif
            };

            var options2 = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls12,
                DangerousAcceptAnyServerCertificate = true,
                ClientCertificate = TestResources.GetTestCertificate(),
                MaxConnectionsPerServer = 20,
#if NET
                RequestHeaderEncoding = Encoding.Latin1
#endif
            };

            var equals = options1.Equals(options2);

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

            var equals = options1.Equals(null);

            Assert.False(equals);
        }
    }
}
