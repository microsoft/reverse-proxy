// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
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

        [Fact]
        public void CreateClient_OldClientExistsNoConfigChange_ReturnsOldInstance()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
            var clientCertificate = TestResources.GetTestCertificate();
            var oldOptions = new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, 10);
            var newOptions = new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, 10);
            var oldMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            var newMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            var context = new ProxyHttpClientContext("cluster1", oldOptions, oldMetadata, oldClient, newOptions, newMetadata);

            var actualClient = factory.CreateClient(context);

            Assert.Equal(newOptions, oldOptions);
            Assert.Same(oldClient, actualClient);
        }

        [Theory]
        [MemberData(nameof(GetChangedMetadata))]
        public void CreateClient_OldClientExistsMetadataChanged_ReturnsNewInstance(Dictionary<string, string> oldMetadata, Dictionary<string, string> newMetadata)
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
            var clientCertificate = TestResources.GetTestCertificate();
            var oldOptions = new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, 10);
            var newOptions = new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, 10);
            var context = new ProxyHttpClientContext("cluster1", oldOptions, oldMetadata, oldClient, newOptions, newMetadata);

            var actualClient = factory.CreateClient(context);

            Assert.Equal(newOptions, oldOptions);
            Assert.NotSame(oldClient, actualClient);
        }

        [Theory]
        [MemberData(nameof(GetChangedHttpClientOptions))]
        public void CreateClient_OldClientExistsHttpClientOptionsChanged_ReturnsNewInstance(ClusterConfig.ClusterProxyHttpClientOptions oldOptions, ClusterConfig.ClusterProxyHttpClientOptions newOptions)
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
            var context = new ProxyHttpClientContext("cluster1", oldOptions, null, oldClient, newOptions, null);

            var actualClient = factory.CreateClient(context);

            Assert.NotEqual(newOptions, oldOptions);
            Assert.NotSame(oldClient, actualClient);
        }

        public static IEnumerable<object[]> GetChangedMetadata()
        {
            return new[]
            {
                new object[] { new Dictionary<string, string>(), new Dictionary<string, string> { { "key1", "value1" } } },
                new object[] { new Dictionary<string, string> { { "key1", "value1" } }, new Dictionary<string, string>() },
                new object[] { new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value1" } } },
                new object[] { new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, new Dictionary<string, string> { { "key3", "value1" }, { "key2", "value2" } } },
                new object[] { new Dictionary<string, string> { { "key1", "value1" } }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value1" } } },
                new object[] { new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value1" } }, new Dictionary<string, string> { { "key1", "value1" } } }
            };
        }

        public static IEnumerable<object[]> GetChangedHttpClientOptions()
        {
            var clientCertificate = TestResources.GetTestCertificate();
            return new[]
            {
                new object[] {
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null),
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, null)
                },
                new object[] {
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null),
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, false, clientCertificate, null)
                },
                new object[] {
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null),
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, null)
                },
                new object[] {
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, null),
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null)
                },
                new object[] {
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, null),
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, 10)
                },
                new object[] {
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, 10),
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null)
                },
                new object[] {
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, 10),
                    new ClusterConfig.ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, 20)
                },
            };
        }

        public static SocketsHttpHandler GetHandler(HttpMessageInvoker client)
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
