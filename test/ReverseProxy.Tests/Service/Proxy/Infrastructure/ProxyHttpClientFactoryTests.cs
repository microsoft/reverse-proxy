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
        public void CreateClient_ApplySslProtocols_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls12 | SslProtocols.Tls13, true, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext("cluster1", default, default, default, options, default));

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, handler.SslOptions.EnabledSslProtocols);
            VerifyDefaultValues(handler, "SslProtocols");
        }

        [Fact]
        public void CreateClient_ApplyValidateRemoteCertificate_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, false, default, default);
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
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, true, certificate, default);
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
            var options = new ClusterConfig.ClusterProxyHttpClientOptions(default, true, default, 22);
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
                ("SslProtocols", h => h.SslOptions.EnabledSslProtocols),
                ("ValidateRemoteCertificate", h => h.SslOptions.RemoteCertificateValidationCallback),
                ("ClientCertificate", h => h.SslOptions.ClientCertificates),
                ("MaxConnectionsPerServer", h => h.MaxConnectionsPerServer)
            };
        }
    }
}
