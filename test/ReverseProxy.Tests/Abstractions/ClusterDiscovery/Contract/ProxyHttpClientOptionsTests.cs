// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Authentication;
using System.Text;
using Xunit;
using Yarp.ReverseProxy.Utilities.Tests;

namespace Yarp.ReverseProxy.Abstractions.Tests
{
    public class ProxyHttpClientOptionsTests
    {
        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                MaxConnectionsPerServer = 20,
                WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true },
#if NET
                RequestHeaderEncoding = Encoding.UTF8.WebName
#endif
            };

            var options2 = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                MaxConnectionsPerServer = 20,
                WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true },
#if NET
                RequestHeaderEncoding = Encoding.UTF8.WebName
#endif
            };

            var equals = options1.Equals(options2);

            Assert.True(equals);
            Assert.True(options1 == options2);
            Assert.False(options1 != options2);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            var options1 = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                MaxConnectionsPerServer = 20,
#if NET
                RequestHeaderEncoding = Encoding.UTF8.WebName
#endif
            };

            var options2 = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls12,
                DangerousAcceptAnyServerCertificate = true,
                MaxConnectionsPerServer = 20,
#if NET
                RequestHeaderEncoding = Encoding.Latin1.WebName
#endif
            };

            var equals = options1.Equals(options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Same_WebProxyAddress_Returns_True()
        {
            var options1 = new HttpClientConfig
            {
                WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true }
            };

            var options2 = new HttpClientConfig
            {
                WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true }
            };

            var equals = options1.Equals(options2);

            Assert.True(equals);
            Assert.True(options1 == options2);
            Assert.False(options1 != options2);
        }

        [Fact]
        public void Equals_Different_WebProxyAddress_Returns_False()
        {
            var options1 = new HttpClientConfig
            {
                WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:8080"), BypassOnLocal = true, UseDefaultCredentials = true }
            };

            var options2 = new HttpClientConfig
            {
                WebProxy = new WebProxyConfig() { Address = new Uri("http://localhost:9999"), BypassOnLocal = true, UseDefaultCredentials = true }
            };

            var equals = options1.Equals(options2);

            Assert.False(equals);
            Assert.True(options1 != options2);
            Assert.False(options1 == options2);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11,
                DangerousAcceptAnyServerCertificate = false,
                MaxConnectionsPerServer = 20
            };

            var equals = options1.Equals(null);

            Assert.False(equals);
        }
    }
}
