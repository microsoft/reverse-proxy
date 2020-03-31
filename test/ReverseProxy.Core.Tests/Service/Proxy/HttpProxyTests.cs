// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
{
    public class HttpProxyTests : TestAutoMockBase
    {
        public HttpProxyTests()
        {
            Provide<IMetricCreator, TestMetricCreator>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<HttpProxy>();
        }

        // Tests normal (as opposed to upgradable) request proxying.
        [Fact]
        public async Task ProxyAsync_NormalRequest_Works()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":host", "example.com");
            httpContext.Request.Headers.Add("x-ms-request-test", "request");
            httpContext.Request.Headers.Add("Content-Language", "requestLanguage");
            httpContext.Request.Body = StringToStream("request content");

            var gatewayResponseStream = new MemoryStream();
            httpContext.Response.Body = gatewayResponseStream;

            var targetUri = new Uri("https://localhost:123/a/b/api/test");
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    request.Version.Should().BeEquivalentTo(new Version(2, 0));
                    request.Method.Should().Be(HttpMethod.Post);
                    request.RequestUri.Should().Be(targetUri);
                    request.Headers.GetValues("x-ms-request-test").Should().BeEquivalentTo("request");

                    request.Content.Should().NotBeNull();
                    request.Content.Headers.GetValues("Content-Language").Should().BeEquivalentTo("requestLanguage");

                    var capturedRequestContent = new MemoryStream();

                    // Use CopyToAsync as this is what HttpClient and friends use internally
                    await request.Content.CopyToAsync(capturedRequestContent);
                    capturedRequestContent.Position = 0;
                    var capturedContentText = StreamToString(capturedRequestContent);
                    capturedContentText.Should().Be("request content");

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    response.ReasonPhrase = "Test Reason Phrase";
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    response.Content = new StreamContent(StringToStream("response content"));
                    response.Content.Headers.TryAddWithoutValidation("Content-Language", "responseLanguage");
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateNormalClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                backendId: "be1",
                routeId: "rt1",
                endpointId: "ep1");

            // Act
            await sut.ProxyAsync(httpContext, targetUri, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            httpContext.Response.StatusCode.Should().Be(234);
            var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
            reasonPhrase.Should().Be("Test Reason Phrase");
            httpContext.Response.Headers["x-ms-response-test"].Should().BeEquivalentTo("response");
            httpContext.Response.Headers["Content-Language"].Should().BeEquivalentTo("responseLanguage");

            gatewayResponseStream.Position = 0;
            var gatewayResponseText = StreamToString(gatewayResponseStream);
            gatewayResponseText.Should().Be("response content");
        }

        // Tests proxying an upgradable request.
        [Fact]
        public async Task ProxyAsync_UpgradableRequest_Works()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":host", "example.com");
            httpContext.Request.Headers.Add("x-ms-request-test", "request");

            var downstreamStream = new DuplexStream(
                readStream: StringToStream("request content"),
                writeStream: new MemoryStream());
            DuplexStream upstreamStream = null;

            var upgradeFeatureMock = new Mock<IHttpUpgradeFeature>();
            upgradeFeatureMock.SetupGet(u => u.IsUpgradableRequest).Returns(true);
            upgradeFeatureMock.Setup(u => u.UpgradeAsync()).ReturnsAsync(downstreamStream);
            httpContext.Features.Set(upgradeFeatureMock.Object);

            var targetUri = new Uri("https://localhost:123/a/b/api/test?a=b&c=d");
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    request.Version.Should().BeEquivalentTo(new Version(1, 1));
                    request.Method.Should().Be(HttpMethod.Get);
                    request.RequestUri.Should().Be(targetUri);
                    request.Headers.GetValues("x-ms-request-test").Should().BeEquivalentTo("request");

                    request.Content.Should().BeNull();

                    var response = new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    upstreamStream = new DuplexStream(
                        readStream: StringToStream("response content"),
                        writeStream: new MemoryStream());
                    response.Content = new RawStreamContent(upstreamStream);
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateUpgradableClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                backendId: "be1",
                routeId: "rt1",
                endpointId: "ep1");

            // Act
            await sut.ProxyAsync(httpContext, targetUri, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            httpContext.Response.StatusCode.Should().Be(StatusCodes.Status101SwitchingProtocols);
            httpContext.Response.Headers["x-ms-response-test"].Should().BeEquivalentTo("response");

            downstreamStream.WriteStream.Position = 0;
            var returnedToDownstream = StreamToString(downstreamStream.WriteStream);
            returnedToDownstream.Should().Be("response content");

            upstreamStream.Should().NotBeNull();
            upstreamStream.WriteStream.Position = 0;
            var sentToUpstream = StreamToString(upstreamStream.WriteStream);
            sentToUpstream.Should().Be("request content");
        }

        // Tests proxying an upgradable request where the upstream refused to upgrade.
        // We should still proxy back the response.
        [Fact]
        public async Task ProxyAsync_UpgradableRequestFailsToUpgrade_ProxiesResponse()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":host", "example.com");
            httpContext.Request.Headers.Add("x-ms-request-test", "request");

            var gatewayResponseStream = new MemoryStream();
            httpContext.Response.Body = gatewayResponseStream;

            var upgradeFeatureMock = new Mock<IHttpUpgradeFeature>(MockBehavior.Strict);
            upgradeFeatureMock.SetupGet(u => u.IsUpgradableRequest).Returns(true);
            httpContext.Features.Set(upgradeFeatureMock.Object);

            var targetUri = new Uri("https://localhost:123/a/b/api/test?a=b&c=d");
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    request.Version.Should().BeEquivalentTo(new Version(1, 1));
                    request.Method.Should().Be(HttpMethod.Get);
                    request.RequestUri.Should().Be(targetUri);
                    request.Headers.GetValues("x-ms-request-test").Should().BeEquivalentTo("request");

                    request.Content.Should().BeNull();

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    response.ReasonPhrase = "Test Reason Phrase";
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    response.Content = new StreamContent(StringToStream("response content"));
                    response.Content.Headers.TryAddWithoutValidation("Content-Language", "responseLanguage");
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateUpgradableClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                backendId: "be1",
                routeId: "rt1",
                endpointId: "ep1");

            // Act
            await sut.ProxyAsync(httpContext, targetUri, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            httpContext.Response.StatusCode.Should().Be(234);
            var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
            reasonPhrase.Should().Be("Test Reason Phrase");
            httpContext.Response.Headers["x-ms-response-test"].Should().BeEquivalentTo("response");
            httpContext.Response.Headers["Content-Language"].Should().BeEquivalentTo("responseLanguage");

            gatewayResponseStream.Position = 0;
            var gatewayResponseText = StreamToString(gatewayResponseStream);
            gatewayResponseText.Should().Be("response content");
        }

        private static MemoryStream StringToStream(string text)
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(text);
            }
            stream.Position = 0;
            return stream;
        }

        private static string StreamToString(MemoryStream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }

        private class MockHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func;

            private MockHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
            {
                this.func = func ?? throw new ArgumentNullException(nameof(func));
            }

            public static HttpMessageInvoker CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
            {
                var handler = new MockHttpHandler(func);
                return new HttpMessageInvoker(handler);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return func(request, cancellationToken);
            }
        }

        private class DuplexStream : Stream
        {
            public DuplexStream(MemoryStream readStream, MemoryStream writeStream)
            {
                Contracts.CheckValue(readStream, nameof(readStream));
                Contracts.CheckValue(writeStream, nameof(writeStream));

                ReadStream = readStream;
                WriteStream = writeStream;
            }

            public MemoryStream ReadStream { get; }

            public MemoryStream WriteStream { get; }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotImplementedException();

            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadStream.Read(buffer, offset, count);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                WriteStream.Write(buffer, offset, count);
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Replacement for <see cref="StreamContent"/> which just returns the raw stream,
        /// whereas <see cref="StreamContent"/> wraps it in a read-only stream.
        /// We need to return the raw internal stream to test full duplex proxying.
        /// </summary>
        private class RawStreamContent : HttpContent
        {
            private readonly Stream stream;

            public RawStreamContent(Stream stream)
            {
                Contracts.CheckValue(stream, nameof(stream));
                this.stream = stream;
            }

            protected override Task<Stream> CreateContentReadStreamAsync()
            {
                return Task.FromResult(stream);
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                throw new NotImplementedException();
            }

            protected override bool TryComputeLength(out long length)
            {
                throw new NotImplementedException();
            }
        }
    }
}
