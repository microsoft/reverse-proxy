// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Telemetry;
using Microsoft.ReverseProxy.Utilities.Tests;
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
            var actual1 = factory.CreateClient(new ProxyHttpClientContext());
            var actual2 = factory.CreateClient(new ProxyHttpClientContext());

            // Assert
            Assert.NotNull(actual1);
            Assert.NotNull(actual2);
            Assert.NotSame(actual2, actual1);
        }

        [Fact]
        public void CreateClient_ApplySslProtocols_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterProxyHttpClientOptions(SslProtocols.Tls12 | SslProtocols.Tls13, default, default, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, handler.SslOptions.EnabledSslProtocols);
            VerifyDefaultValues(handler, "SslProtocols");
        }

        [Fact]
        public void CreateClient_ApplyDangerousAcceptAnyServerCertificate_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterProxyHttpClientOptions(default, true, default, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.NotNull(handler.SslOptions.RemoteCertificateValidationCallback);
            Assert.True(handler.SslOptions.RemoteCertificateValidationCallback(default, default, default, default));
            VerifyDefaultValues(handler, "DangerousAcceptAnyServerCertificate");
        }

        [Fact]
        public void CreateClient_ApplyClientCertificate_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var certificate = TestResources.GetTestCertificate();
            var options = new ClusterProxyHttpClientOptions(default, default, certificate, default, default);
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

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
            var options = new ClusterProxyHttpClientOptions(default, default, default, 22, default);
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.Equal(22, handler.MaxConnectionsPerServer);
            VerifyDefaultValues(handler, "MaxConnectionsPerServer");
        }

        [Fact]
        public void CreateClient_ApplyPropagateActivityContext_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ClusterProxyHttpClientOptions(default, default, default, default, true);
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            // TODO Assert.Equal(true, handler.prop);
            VerifyDefaultValues(handler, "MaxConnectionsPerServer");
        }

        [Fact]
        public void CreateClient_OldClientExistsNoConfigChange_ReturnsOldInstance()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
            var clientCertificate = TestResources.GetTestCertificate();
            var oldOptions = new ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, 10, true);
            var newOptions = new ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, 10, true);
            var oldMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            var newMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            var context = new ProxyHttpClientContext { ClusterId = "cluster1", OldOptions = oldOptions, OldMetadata = oldMetadata, OldClient = oldClient, NewOptions = newOptions, NewMetadata = newMetadata };

            var actualClient = factory.CreateClient(context);

            Assert.Equal(newOptions, oldOptions);
            Assert.Same(oldClient, actualClient);
        }

        [Theory]
        [MemberData(nameof(GetChangedHttpClientOptions))]
        public void CreateClient_OldClientExistsHttpClientOptionsChanged_ReturnsNewInstance(ClusterProxyHttpClientOptions oldOptions, ClusterProxyHttpClientOptions newOptions)
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
            var context = new ProxyHttpClientContext { ClusterId = "cluster1", OldOptions = oldOptions, OldClient = oldClient, NewOptions = newOptions };

            var actualClient = factory.CreateClient(context);

            Assert.NotEqual(newOptions, oldOptions);
            Assert.NotSame(oldClient, actualClient);
        }

        public static IEnumerable<object[]> GetChangedHttpClientOptions()
        {
            var clientCertificate = TestResources.GetTestCertificate();
            return new[]
            {
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null, false),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11 | SslProtocols.Tls12, true, clientCertificate, null, false)
                },
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null, false),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, false, clientCertificate, null, false)
                },
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null, false),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, null, false)
                },
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, null, false),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null, false)
                },
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, null, false),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, 10, false)
                },
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, 10, false),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, null, false)
                },
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, 10, false),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, clientCertificate, 20, false)
                },
                new object[] {
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, 10, true),
                    new ClusterProxyHttpClientOptions(SslProtocols.Tls11, true, null, 10, false)
                }
            };
        }

        public static SocketsHttpHandler GetHandler(HttpMessageInvoker client)
        {
            var handlerFieldInfo = typeof(HttpMessageInvoker).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(f => f.Name == "_handler");
            var diagnosticsHandler = (DiagnosticsHandler)handlerFieldInfo.GetValue(client);
            var result = (SocketsHttpHandler)diagnosticsHandler.InnerHandler;
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
                ("DangerousAcceptAnyServerCertificate", h => h.SslOptions.RemoteCertificateValidationCallback),
                ("ClientCertificate", h => h.SslOptions.ClientCertificates),
                ("MaxConnectionsPerServer", h => h.MaxConnectionsPerServer)
            };
        }
    }
}
