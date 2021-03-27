// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.Service.Proxy.Infrastructure;
using Yarp.ReverseProxy.Telemetry;
using Yarp.ReverseProxy.Utilities.Tests;

namespace Yarp.ReverseProxy.Service.Proxy.Tests
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
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);

            var actual1 = factory.CreateClient(new ProxyHttpClientContext()
            {
                NewOptions = ProxyHttpClientOptions.Empty,
                OldOptions = ProxyHttpClientOptions.Empty
            });
            var actual2 = factory.CreateClient(new ProxyHttpClientContext()
            {
                NewOptions = ProxyHttpClientOptions.Empty,
                OldOptions = ProxyHttpClientOptions.Empty
            });

            Assert.NotNull(actual1);
            Assert.NotNull(actual2);
            Assert.NotSame(actual2, actual1);
        }

        [Fact]
        public void CreateClient_ApplySslProtocols_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            };
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
            var options = new ProxyHttpClientOptions { DangerousAcceptAnyServerCertificate = true };
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
            var options = new ProxyHttpClientOptions { ClientCertificate = certificate };
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
            var options = new ProxyHttpClientOptions { MaxConnectionsPerServer = 22 };
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
            var options = new ProxyHttpClientOptions { ActivityContextHeaders = ActivityContextHeaders.None };
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client, expectActivityPropagationHandler: false);

            Assert.NotNull(handler);
        }

        [Fact]
        public void CreateClient_ApplyWebProxy_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var mockWebProxy = Mock<System.Net.IWebProxy>().Object;
            var options = new ProxyHttpClientOptions { WebProxy = mockWebProxy };
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.NotNull(handler.Proxy);
            Assert.True(handler.UseProxy);
            VerifyDefaultValues(handler, "WebProxy");
        }

#if NET
        [Fact]
        public void CreateClient_ApplyRequestHeaderEncoding_Success()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ProxyHttpClientOptions
            {
                RequestHeaderEncoding = Encoding.Latin1
            };
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client);

            Assert.NotNull(handler);
            Assert.NotNull(handler.RequestHeaderEncodingSelector);
            Assert.Equal(Encoding.Latin1, handler.RequestHeaderEncodingSelector(default, default));
            VerifyDefaultValues(handler, nameof(SocketsHttpHandler.RequestHeaderEncodingSelector));
        }
#endif

        [Fact]
        public void CreateClient_OldClientExistsNoConfigChange_ReturnsOldInstance()
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
            var clientCertificate = TestResources.GetTestCertificate();
            var oldOptions = new ProxyHttpClientOptions
            {
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                DangerousAcceptAnyServerCertificate = true,
                ClientCertificate = clientCertificate,
                MaxConnectionsPerServer = 10,
                ActivityContextHeaders = ActivityContextHeaders.CorrelationContext,
#if NET
                RequestHeaderEncoding = Encoding.Latin1,
#endif
            };
            var newOptions = oldOptions with { }; // Clone
            var oldMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            var newMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            var context = new ProxyHttpClientContext { ClusterId = "cluster1", OldOptions = oldOptions, OldMetadata = oldMetadata, OldClient = oldClient, NewOptions = newOptions, NewMetadata = newMetadata };

            var actualClient = factory.CreateClient(context);

            Assert.Equal(newOptions, oldOptions);
            Assert.Same(oldClient, actualClient);
        }

#if NET
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CreateClient_ApplyEnableMultipleHttp2Connections_Success(bool enableMultipleHttp2Connections)
        {
            var factory = new ProxyHttpClientFactory(Mock<ILogger<ProxyHttpClientFactory>>().Object);
            var options = new ProxyHttpClientOptions {  EnableMultipleHttp2Connections = enableMultipleHttp2Connections };
            var client = factory.CreateClient(new ProxyHttpClientContext { NewOptions = options });

            var handler = GetHandler(client);

            Assert.Equal(enableMultipleHttp2Connections, handler.EnableMultipleHttp2Connections);
        }
#endif

        [Theory]
        [MemberData(nameof(GetChangedHttpClientOptions))]
        public void CreateClient_OldClientExistsHttpClientOptionsChanged_ReturnsNewInstance(ProxyHttpClientOptions oldOptions, ProxyHttpClientOptions newOptions)
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
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = false,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = false,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = false,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = null,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = clientCertificate,
                        MaxConnectionsPerServer = 20,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.CorrelationContext,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.Baggage,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.CorrelationContext,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.CorrelationContext,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.BaggageAndCorrelationContext,
                    },
                },
#if NET
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.Baggage,
                        EnableMultipleHttp2Connections = true
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.Baggage,
                        EnableMultipleHttp2Connections = false
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                        RequestHeaderEncoding = Encoding.UTF8,
                    },
                },
                new object[] {
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                        RequestHeaderEncoding = Encoding.UTF8,
                    },
                    new ProxyHttpClientOptions
                    {
                        SslProtocols = SslProtocols.Tls11,
                        DangerousAcceptAnyServerCertificate = true,
                        ClientCertificate = null,
                        MaxConnectionsPerServer = 10,
                        ActivityContextHeaders = ActivityContextHeaders.None,
                        RequestHeaderEncoding = Encoding.Latin1,
                    },
                }
#endif
            };
        }

        public static SocketsHttpHandler GetHandler(HttpMessageInvoker client, bool expectActivityPropagationHandler = true)
        {
            var handlerFieldInfo = typeof(HttpMessageInvoker).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(f => f.Name == "_handler");
            var handler = handlerFieldInfo.GetValue(client);

            if (handler is ActivityPropagationHandler diagnosticsHandler)
            {
                Assert.True(expectActivityPropagationHandler);
                return (SocketsHttpHandler)diagnosticsHandler.InnerHandler;
            }

            Assert.False(expectActivityPropagationHandler);
            return (SocketsHttpHandler)handler;
        }

        private void VerifyDefaultValues(SocketsHttpHandler actualHandler, params string[] skippedExtractors)
        {
            var skippedSet = new HashSet<string>(skippedExtractors);
            var defaultHandler = new SocketsHttpHandler();
            foreach (var extractor in GetAllExtractors().Where(e => !skippedSet.Contains(e.name)).Select(e => e.extractor))
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
                ("MaxConnectionsPerServer", h => h.MaxConnectionsPerServer),
                ("WebProxy", h => h.Proxy)
            };
        }
    }
}
