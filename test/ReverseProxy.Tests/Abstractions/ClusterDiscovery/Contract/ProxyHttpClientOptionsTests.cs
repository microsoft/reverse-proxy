// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
                SslApplicationProtocols = new List<string> { "Http11", "Http2" },
                RevocationCheckMode = X509RevocationMode.Online,
                CipherSuitesPolicy = new List<TlsCipherSuite> { TlsCipherSuite.TLS_AES_128_CCM_SHA256, TlsCipherSuite.TLS_AES_256_GCM_SHA384 },
                SslProtocols = new List<SslProtocols> { SslProtocols.Tls12, SslProtocols.Tls13},
                EncryptionPolicy = EncryptionPolicy.AllowNoEncryption,
                MaxConnectionsPerServer = 10
            };

            // Act
            var clone = options.DeepClone();

            // Assert
            Assert.NotSame(options, clone);
            Assert.NotSame(options.SslApplicationProtocols, clone.SslApplicationProtocols);
            Assert.Equal(options.SslApplicationProtocols.Count, clone.SslApplicationProtocols.Count);
            Assert.Equal(options.SslApplicationProtocols[0], clone.SslApplicationProtocols[0]);
            Assert.Equal(options.SslApplicationProtocols[1], clone.SslApplicationProtocols[1]);
            Assert.Equal(options.RevocationCheckMode, clone.RevocationCheckMode);
            Assert.NotSame(options.CipherSuitesPolicy, clone.CipherSuitesPolicy);
            Assert.Equal(options.CipherSuitesPolicy.Count, clone.CipherSuitesPolicy.Count);
            Assert.Equal(options.CipherSuitesPolicy[0], clone.CipherSuitesPolicy[0]);
            Assert.Equal(options.CipherSuitesPolicy[1], clone.CipherSuitesPolicy[1]);
            Assert.Equal(options.EncryptionPolicy, clone.EncryptionPolicy);
            Assert.Equal(options.ClientCertificate, clone.ClientCertificate);
            Assert.Equal(options.MaxConnectionsPerServer, clone.MaxConnectionsPerServer);
        }
    }
}
