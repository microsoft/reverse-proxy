// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Utilities.Tests;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
{
    public class ProxyHttpClientFactoryTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
        }

        [Fact]
        public void CreateClient_Works()
        {
            // Arrange
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);

            // Act
            var actual1 = factory.CreateClient(new ProxyHttpClientContext(default, default, default, default, default, default));
            var actual2 = factory.CreateClient(new ProxyHttpClientContext(default, default, default, default, default, default));

            // Assert
            Assert.NotNull(actual1);
            Assert.NotNull(actual2);
            Assert.NotSame(actual2, actual1);
        }

        [Fact]
        public void CreateClient_ApplySslApplicationProtocols_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(new[] { SslApplicationProtocol.Http11, SslApplicationProtocol.Http2 }, default, default, default, default, true, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.NotNull(handler.SslOptions.ApplicationProtocols);
            Assert.Equal(2, handler.SslOptions.ApplicationProtocols.Count);
            Assert.Contains(SslApplicationProtocol.Http11, handler.SslOptions.ApplicationProtocols);
            Assert.Contains(SslApplicationProtocol.Http2, handler.SslOptions.ApplicationProtocols);
            VerifyDefaultValues(handler, "SslApplicationProtocols");
        }

        [Fact]
        public void CreateClient_ApplyRevocationCheckMode_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, X509RevocationMode.Offline, default, default, default, true, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Equal(X509RevocationMode.Offline, handler.SslOptions.CertificateRevocationCheckMode);
            VerifyDefaultValues(handler, "RevocationCheckMode");
        }

        [Fact]
        public void CreateClient_ApplyCipherSuitesPolicy_Success()
        {
            // CipherSuitesPolicy is not supported on Windows.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, default, new CipherSuitesPolicy(new[] { TlsCipherSuite.TLS_AES_128_CCM_SHA256, TlsCipherSuite.TLS_AES_256_GCM_SHA384}), default, default, true, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.NotNull(handler.SslOptions.CipherSuitesPolicy);
            Assert.Equal(2, handler.SslOptions.CipherSuitesPolicy.AllowedCipherSuites.Count());
            Assert.Contains(TlsCipherSuite.TLS_AES_128_CCM_SHA256, handler.SslOptions.CipherSuitesPolicy.AllowedCipherSuites);
            Assert.Contains(TlsCipherSuite.TLS_AES_256_GCM_SHA384, handler.SslOptions.CipherSuitesPolicy.AllowedCipherSuites);
            VerifyDefaultValues(handler, "CipherSuitesPolicy");
        }

        [Fact]
        public void CreateClient_ApplySslProtocols_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, default, default, SslProtocols.Tls12 | SslProtocols.Tls13, default, true, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, handler.SslOptions.EnabledSslProtocols);
            VerifyDefaultValues(handler, "SslProtocols");
        }

        [Fact]
        public void CreateClient_ApplyEncryptionPolicy_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, default, default, default, EncryptionPolicy.AllowNoEncryption, true, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Equal(EncryptionPolicy.AllowNoEncryption, handler.SslOptions.EncryptionPolicy);
            VerifyDefaultValues(handler, "EncryptionPolicy");
        }

        [Fact]
        public void CreateClient_ApplyEValidateRemoteCertificate_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, default, default, default, default, false, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.NotNull(handler.SslOptions.RemoteCertificateValidationCallback);
            Assert.True(handler.SslOptions.RemoteCertificateValidationCallback(default, default, default, default));
            VerifyDefaultValues(handler, "ValidateRemoteCertificate");
        }

        [Fact]
        public void CreateClient_ApplyClientCertificate_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var certificate = TestResources.GetTestCertificate();
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, default, default, default, default, true, certificate, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Single(handler.SslOptions.ClientCertificates);
            Assert.Single(handler.SslOptions.ClientCertificates, certificate);
            VerifyDefaultValues(handler, "ClientCertificate");
        }

        [Fact]
        public void CreateClient_ApplyMaxConnectionsPerServer_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, default, default, default, default, true, default, 22);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Equal(22, handler.MaxConnectionsPerServer);
            VerifyDefaultValues(handler, "MaxConnectionsPerServer");
        }

        private static SocketsHttpHandler GetHandler(HttpMessageInvoker client)
        {
            var handlerFieldInfo = typeof(HttpMessageInvoker).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(f => f.Name == "_handler");
            var result = (SocketsHttpHandler)handlerFieldInfo.GetValue(client);
            return result;
        }

        private void VerifyDefaultValues(SocketsHttpHandler actualHandler, params string[] skippedExtractors)
        {
            var skippedSet = new HashSet<string>(skippedExtractors);
            var defaultHandler = new SocketsHttpHandler();
            foreach(var extractor in GetAllExtractors().Where(e => !skippedSet.Contains(e.name)).Select(e => e.extractor))
            {
                Assert.Equal(extractor(defaultHandler), extractor(actualHandler));
            }
        }

        private (string name, Func<SocketsHttpHandler, object> extractor)[] GetAllExtractors()
        {
            return new (string name, Func<SocketsHttpHandler, object> extractor)[] {
                ("SslApplicationProtocols", h => h.SslOptions.ApplicationProtocols),
                ("RevocationCheckMode", h => h.SslOptions.CertificateRevocationCheckMode),
                ("CipherSuitesPolicy", h => h.SslOptions.CipherSuitesPolicy),
                ("SslProtocols", h => h.SslOptions.EnabledSslProtocols),
                ("EncryptionPolicy", h => h.SslOptions.EncryptionPolicy),
                ("ValidateRemoteCertificate", h => h.SslOptions.RemoteCertificateValidationCallback),
                ("ClientCertificate", h => h.SslOptions.ClientCertificates),
                ("MaxConnectionsPerServer", h => h.MaxConnectionsPerServer)
            };
        }
    }
}
