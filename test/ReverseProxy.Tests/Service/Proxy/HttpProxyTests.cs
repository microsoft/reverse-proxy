// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
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
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Path = "/path/base/dropped";
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":authority", "example.com:3456");
            httpContext.Request.Headers.Add("x-ms-request-test", "request");
            httpContext.Request.Headers.Add("Content-Language", "requestLanguage");
            httpContext.Request.Headers.Add("Content-Length", "1");
            httpContext.Request.Body = StringToStream("request content");
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/a/b/";
            var targetUri = "https://localhost:123/a/b/api/test?a=b&c=d";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    Assert.Equal(new Version(2, 0), request.Version);
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal(targetUri, request.RequestUri.AbsoluteUri);
                    Assert.Contains("request", request.Headers.GetValues("x-ms-request-test"));
                    Assert.Equal("example.com:3456", request.Headers.Host);
                    Assert.False(request.Headers.TryGetValues(":authority", out var value));

                    Assert.NotNull(request.Content);
                    Assert.Contains("requestLanguage", request.Content.Headers.GetValues("Content-Language"));

                    var capturedRequestContent = new MemoryStream();

                    // Use CopyToAsync as this is what HttpClient and friends use internally
                    await request.Content.CopyToAsync(capturedRequestContent);
                    capturedRequestContent.Position = 0;
                    var capturedContentText = StreamToString(capturedRequestContent);
                    Assert.Equal("request content", capturedContentText);

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    response.ReasonPhrase = "Test Reason Phrase";
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    response.Content = new StreamContent(StringToStream("response content"));
                    response.Content.Headers.TryAddWithoutValidation("Content-Language", "responseLanguage");
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            // Act
            await sut.ProxyAsync(httpContext, destinationPrefix, Transforms.Empty, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.Equal(234, httpContext.Response.StatusCode);
            var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
            Assert.Equal("Test Reason Phrase", reasonPhrase);
            Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());
            Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());

            proxyResponseStream.Position = 0;
            var proxyResponseText = StreamToString(proxyResponseStream);
            Assert.Equal("response content", proxyResponseText);
        }

        [Fact]
        public async Task ProxyAsync_NormalRequestWithTransforms_Works()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Protocol = "http/2";
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Path = "/path/base/dropped";
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":authority", "example.com:3456");
            httpContext.Request.Headers.Add("x-ms-request-test", "request");
            httpContext.Request.Headers.Add("Content-Language", "requestLanguage");
            httpContext.Request.Body = StringToStream("request content");
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
            httpContext.Features.Set<IHttpResponseTrailersFeature>(new TestTrailersFeature());

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/a/b/";
            var transforms = new Transforms(copyRequestHeaders: true,
            requestTransforms: new[]
            {
                new PathStringTransform(PathStringTransform.PathTransformMode.Prefix, "/prefix"),
            },
            requestHeaderTransforms: new Dictionary<string, RequestHeaderTransform>(StringComparer.OrdinalIgnoreCase)
            {
                { "transformHeader", new RequestHeaderValueTransform("value", append: false) },
                { "x-ms-request-test", new RequestHeaderValueTransform("transformValue", append: true) },
                { HeaderNames.Host, new RequestHeaderValueTransform(string.Empty, append: false) } // Default, remove Host
            },
            responseHeaderTransforms: new Dictionary<string, ResponseHeaderTransform>(StringComparer.OrdinalIgnoreCase)
            {
                { "transformHeader", new ResponseHeaderValueTransform("value", append: false, always: true) },
                { "x-ms-response-test", new ResponseHeaderValueTransform("value", append: true, always: false) }
            },
            responseTrailerTransforms: new Dictionary<string, ResponseHeaderTransform>(StringComparer.OrdinalIgnoreCase)
            {
                { "trailerTransform", new ResponseHeaderValueTransform("value", append: false, always: true) }
            });
            var targetUri = "https://localhost:123/a/b/prefix/api/test?a=b&c=d";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    Assert.Equal(new Version(2, 0), request.Version);
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal(targetUri, request.RequestUri.AbsoluteUri);
                    Assert.Equal(new[] { "value" }, request.Headers.GetValues("transformHeader"));
                    Assert.Equal(new[] { "request", "transformValue" }, request.Headers.GetValues("x-ms-request-test"));
                    Assert.Null(request.Headers.Host);
                    Assert.False(request.Headers.TryGetValues(":authority", out var value));

                    Assert.NotNull(request.Content);
                    Assert.Contains("requestLanguage", request.Content.Headers.GetValues("Content-Language"));

                    var capturedRequestContent = new MemoryStream();

                    // Use CopyToAsync as this is what HttpClient and friends use internally
                    await request.Content.CopyToAsync(capturedRequestContent);
                    capturedRequestContent.Position = 0;
                    var capturedContentText = StreamToString(capturedRequestContent);
                    Assert.Equal("request content", capturedContentText);

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    response.ReasonPhrase = "Test Reason Phrase";
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    response.Content = new StreamContent(StringToStream("response content"));
                    response.Content.Headers.TryAddWithoutValidation("Content-Language", "responseLanguage");
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            // Act
            await sut.ProxyAsync(httpContext, destinationPrefix, transforms: transforms, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.Equal(234, httpContext.Response.StatusCode);
            var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
            Assert.Equal("Test Reason Phrase", reasonPhrase);
            Assert.Equal(new[] { "response", "value" }, httpContext.Response.Headers["x-ms-response-test"].ToArray());
            Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());
            Assert.Contains("value", httpContext.Response.Headers["transformHeader"].ToArray());
            Assert.Equal(new[] { "value" }, httpContext.Features.Get<IHttpResponseTrailersFeature>().Trailers?["trailerTransform"].ToArray());

            proxyResponseStream.Position = 0;
            var proxyResponseText = StreamToString(proxyResponseStream);
            Assert.Equal("response content", proxyResponseText);
        }

        [Fact]
        public async Task ProxyAsync_NormalRequestWithCopyRequestHeadersDisabled_Works()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.PathBase = "/api";
            httpContext.Request.Path = "/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":authority", "example.com:3456");
            httpContext.Request.Headers.Add("x-ms-request-test", "request");
            httpContext.Request.Headers.Add("Content-Language", "requestLanguage");
            httpContext.Request.Headers.Add("Transfer-Encoding", "chunked");
            httpContext.Request.Body = StringToStream("request content");
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/a/b/";
            var transforms = new Transforms(copyRequestHeaders: false,
                requestTransforms: Array.Empty<RequestParametersTransform>(),
                requestHeaderTransforms: new Dictionary<string, RequestHeaderTransform>(StringComparer.OrdinalIgnoreCase)
                {
                    { "transformHeader", new RequestHeaderValueTransform("value", append: false) },
                    { "x-ms-request-test", new RequestHeaderValueTransform("transformValue", append: true) },
                    // Defaults
                    { "x-forwarded-for", new RequestHeaderXForwardedForTransform(append: true) },
                    { "x-forwarded-host", new RequestHeaderXForwardedHostTransform(append: true) },
                    { "x-forwarded-proto", new RequestHeaderXForwardedProtoTransform(append: true) },
                    { "x-forwarded-pathbase", new RequestHeaderXForwardedPathBaseTransform(append: true) },
                },
                responseHeaderTransforms: new Dictionary<string, ResponseHeaderTransform>(StringComparer.OrdinalIgnoreCase),
                responseTrailerTransforms: new Dictionary<string, ResponseHeaderTransform>(StringComparer.OrdinalIgnoreCase));
            var targetUri = "https://localhost:123/a/b/test?a=b&c=d";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    Assert.Equal(new Version(2, 0), request.Version);
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal(targetUri, request.RequestUri.AbsoluteUri);
                    Assert.Equal(new[] { "value" }, request.Headers.GetValues("transformHeader"));
                    Assert.Equal(new[] { "request", "transformValue" }, request.Headers.GetValues("x-ms-request-test"));
                    Assert.Null(request.Headers.Host);
                    Assert.False(request.Headers.TryGetValues(":authority", out var _));
                    Assert.Equal("127.0.0.1", request.Headers.GetValues("x-forwarded-for").Single());
                    Assert.Equal("example.com:3456", request.Headers.GetValues("x-forwarded-host").Single());
                    Assert.Equal("http", request.Headers.GetValues("x-forwarded-proto").Single());
                    Assert.Equal("/api", request.Headers.GetValues("x-forwarded-pathbase").Single());

                    Assert.NotNull(request.Content);
                    Assert.False(request.Content.Headers.TryGetValues("Content-Language", out var _));

                    var capturedRequestContent = new MemoryStream();

                    // Use CopyToAsync as this is what HttpClient and friends use internally
                    await request.Content.CopyToAsync(capturedRequestContent);
                    capturedRequestContent.Position = 0;
                    var capturedContentText = StreamToString(capturedRequestContent);
                    Assert.Equal("request content", capturedContentText);

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    response.ReasonPhrase = "Test Reason Phrase";
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    response.Content = new StreamContent(StringToStream("response content"));
                    response.Content.Headers.TryAddWithoutValidation("Content-Language", "responseLanguage");
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            // Act
            await sut.ProxyAsync(httpContext, destinationPrefix, transforms: transforms, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.Equal(234, httpContext.Response.StatusCode);
            var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
            Assert.Equal("Test Reason Phrase", reasonPhrase);
            Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());
            Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());

            proxyResponseStream.Position = 0;
            var proxyResponseText = StreamToString(proxyResponseStream);
            Assert.Equal("response content", proxyResponseText);
        }

        [Fact]
        public async Task ProxyAsync_NormalRequestWithExistingForwarders_Appends()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.PathBase = "/pathbase";
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":authority", "example.com:3456");
            httpContext.Request.Headers.Add("x-forwarded-for", "::1");
            httpContext.Request.Headers.Add("x-forwarded-proto", "https");
            httpContext.Request.Headers.Add("x-forwarded-host", "some.other.host:4567");
            httpContext.Request.Headers.Add("x-forwarded-pathbase", "/other");
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            var transforms = new Transforms(copyRequestHeaders: false,
                requestTransforms: Array.Empty<RequestParametersTransform>(),
                requestHeaderTransforms: new Dictionary<string, RequestHeaderTransform>(StringComparer.OrdinalIgnoreCase)
                {
                    // Defaults
                    { HeaderNames.Host, new RequestHeaderValueTransform(string.Empty, append: false) }, // Default, remove Host
                    { "x-forwarded-for", new RequestHeaderXForwardedForTransform(append: true) },
                    { "x-forwarded-host", new RequestHeaderXForwardedHostTransform(append: true) },
                    { "x-forwarded-proto", new RequestHeaderXForwardedProtoTransform(append: true) },
                    { "x-forwarded-pathbase", new RequestHeaderXForwardedPathBaseTransform(append: true) },
                },
                responseHeaderTransforms: new Dictionary<string, ResponseHeaderTransform>(StringComparer.OrdinalIgnoreCase),
                responseTrailerTransforms: new Dictionary<string, ResponseHeaderTransform>(StringComparer.OrdinalIgnoreCase));

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/a/b/";
            var targetUri = "https://localhost:123/a/b/api/test?a=b&c=d";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    Assert.Equal(new Version(2, 0), request.Version);
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal(targetUri, request.RequestUri.AbsoluteUri);
                    Assert.Equal(new[] { "::1", "127.0.0.1" }, request.Headers.GetValues("x-forwarded-for"));
                    Assert.Equal(new[] { "https", "http" }, request.Headers.GetValues("x-forwarded-proto"));
                    Assert.Equal(new[] { "some.other.host:4567", "example.com:3456" }, request.Headers.GetValues("x-forwarded-host"));
                    Assert.Equal(new[] { "/other", "/pathbase" }, request.Headers.GetValues("x-forwarded-pathbase"));
                    Assert.Null(request.Headers.Host);
                    Assert.False(request.Headers.TryGetValues(":authority", out var value));

                    // The proxy throws if the request body is not read.
                    await (request.Content?.CopyToAsync(Stream.Null) ?? Task.CompletedTask);

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            // Act
            await sut.ProxyAsync(httpContext, destinationPrefix, transforms, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.Equal(234, httpContext.Response.StatusCode);
        }

        // Tests proxying an upgradable request.
        [Fact]
        public async Task ProxyAsync_UpgradableRequest_Works()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");
            httpContext.Request.Headers.Add(":authority", "example.com:3456");
            httpContext.Request.Headers.Add("x-ms-request-test", "request");
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            // TODO: https://github.com/microsoft/reverse-proxy/issues/255
            httpContext.Request.Headers.Add("Upgrade", "WebSocket");

            var downstreamStream = new DuplexStream(
                readStream: StringToStream("request content"),
                writeStream: new MemoryStream());
            DuplexStream upstreamStream = null;

            var upgradeFeatureMock = new Mock<IHttpUpgradeFeature>();
            upgradeFeatureMock.SetupGet(u => u.IsUpgradableRequest).Returns(true);
            upgradeFeatureMock.Setup(u => u.UpgradeAsync()).ReturnsAsync(downstreamStream);
            httpContext.Features.Set(upgradeFeatureMock.Object);

            var destinationPrefix = "https://localhost:123/a/b/";
            var targetUri = "https://localhost:123/a/b/api/test?a=b&c=d";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    Assert.Equal(new Version(1, 1), request.Version);
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal(targetUri, request.RequestUri.AbsoluteUri);
                    Assert.Contains("request", request.Headers.GetValues("x-ms-request-test"));
                    Assert.Equal("example.com:3456", request.Headers.Host);
                    Assert.False(request.Headers.TryGetValues(":authority", out var value));

                    Assert.Null(request.Content);

                    var response = new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    upstreamStream = new DuplexStream(
                        readStream: StringToStream("response content"),
                        writeStream: new MemoryStream());
                    response.Content = new RawStreamContent(upstreamStream);
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            // Act
            await sut.ProxyAsync(httpContext, destinationPrefix, Transforms.Empty, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.Equal(StatusCodes.Status101SwitchingProtocols, httpContext.Response.StatusCode);
            Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());

            downstreamStream.WriteStream.Position = 0;
            var returnedToDownstream = StreamToString(downstreamStream.WriteStream);
            Assert.Equal("response content", returnedToDownstream);

            Assert.NotNull(upstreamStream);
            upstreamStream.WriteStream.Position = 0;
            var sentToUpstream = StreamToString(upstreamStream.WriteStream);
            Assert.Equal("request content", sentToUpstream);
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

            // TODO: https://github.com/microsoft/reverse-proxy/issues/255
            httpContext.Request.Headers.Add("Upgrade", "WebSocket");

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var upgradeFeatureMock = new Mock<IHttpUpgradeFeature>(MockBehavior.Strict);
            upgradeFeatureMock.SetupGet(u => u.IsUpgradableRequest).Returns(true);
            httpContext.Features.Set(upgradeFeatureMock.Object);

            var destinationPrefix = "https://localhost:123/a/b/";
            var targetUri = "https://localhost:123/a/b/api/test?a=b&c=d";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();

                    Assert.Equal(new Version(1, 1), request.Version);
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal(targetUri, request.RequestUri.AbsoluteUri);
                    Assert.Contains("request", request.Headers.GetValues("x-ms-request-test"));

                    Assert.Null(request.Content);

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    response.ReasonPhrase = "Test Reason Phrase";
                    response.Headers.TryAddWithoutValidation("x-ms-response-test", "response");
                    response.Content = new StreamContent(StringToStream("response content"));
                    response.Content.Headers.TryAddWithoutValidation("Content-Language", "responseLanguage");
                    return response;
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            // Act
            await sut.ProxyAsync(httpContext, destinationPrefix, Transforms.Empty, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.Equal(234, httpContext.Response.StatusCode);
            var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
            Assert.Equal("Test Reason Phrase", reasonPhrase);
            Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());
            Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());

            proxyResponseStream.Position = 0;
            var proxyResponseText = StreamToString(proxyResponseStream);
            Assert.Equal("response content", proxyResponseText);
        }

        [Theory]
        [InlineData("TRACE", "HTTP/1.1", "")]
        [InlineData("TRACE", "HTTP/2", "")]
        [InlineData("TRACE", "HTTP/1.1", "Content-Length:10")]
        [InlineData("TRACE", "HTTP/1.1", "Transfer-Encoding:chunked")]
        [InlineData("TRACE", "HTTP/1.1", "Expect:100-continue")]
        [InlineData("GET", "HTTP/1.1", "")]
        [InlineData("GET", "HTTP/2", "")]
        [InlineData("GET", "HTTP/1.1", "Content-Length:0")]
        [InlineData("HEAD", "HTTP/1.1", "")]
        [InlineData("POST", "HTTP/1.1", "")]
        [InlineData("POST", "HTTP/1.1", "Content-Length:0")]
        [InlineData("POST", "HTTP/2", "Content-Length:0")]
        [InlineData("PATCH", "HTTP/1.1", "")]
        [InlineData("DELETE", "HTTP/1.1", "")]
        [InlineData("Delete", "HTTP/1.1", "expect:100-continue")]
        [InlineData("Unknown", "HTTP/1.1", "")]
        // [InlineData("CONNECT", "HTTP/1.1", "")] Blocked in HttpUtilities.GetHttpMethod
        public async Task ProxyAsync_RequetsWithoutBodies_NoHttpContent(string method, string protocol, string headers)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = method;
            httpContext.Request.Protocol = protocol;
            foreach (var header in headers.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = header.Split(':');
                var key = parts[0];
                var value = parts[1];
                httpContext.Request.Headers[key] = value;
            }

            var destinationPrefix = "https://localhost/";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    Assert.Equal(new Version(2, 0), request.Version);
                    Assert.Equal(method, request.Method.Method, StringComparer.OrdinalIgnoreCase);

                    Assert.Null(request.Content);

                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                    return Task.FromResult(response);
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            await sut.ProxyAsync(httpContext, destinationPrefix, Transforms.Empty, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        }

        [Theory]
        [InlineData("POST", "HTTP/2", "")]
        [InlineData("PATCH", "HTTP/2", "")]
        [InlineData("UNKNOWN", "HTTP/2", "")]
        [InlineData("UNKNOWN", "HTTP/1.1", "Content-Length:10")]
        [InlineData("UNKNOWN", "HTTP/1.1", "transfer-encoding:Chunked")]
        [InlineData("GET", "HTTP/1.1", "Content-Length:10")]
        [InlineData("GET", "HTTP/2", "Content-Length:10")]
        [InlineData("HEAD", "HTTP/1.1", "transfer-encoding:Chunked")]
        [InlineData("HEAD", "HTTP/2", "transfer-encoding:Chunked")]
        [InlineData("Delete", "HTTP/2", "expect:100-continue")]
        public async Task ProxyAsync_RequetsWithBodies_HasHttpContent(string method, string protocol, string headers)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = method;
            httpContext.Request.Protocol = protocol;
            foreach (var header in headers.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = header.Split(':');
                var key = parts[0];
                var value = parts[1];
                httpContext.Request.Headers[key] = value;
            }

            var destinationPrefix = "https://localhost/";
            var sut = Create<HttpProxy>();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    Assert.Equal(new Version(2, 0), request.Version);
                    Assert.Equal(method, request.Method.Method, StringComparer.OrdinalIgnoreCase);

                    Assert.NotNull(request.Content);

                    // Must consume the body
                    await request.Content.CopyToAsync(Stream.Null);

                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                });
            var factoryMock = new Mock<IProxyHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient()).Returns(client);

            var proxyTelemetryContext = new ProxyTelemetryContext(
                clusterId: "be1",
                routeId: "rt1",
                destinationId: "d1");

            await sut.ProxyAsync(httpContext, destinationPrefix, Transforms.Empty, factoryMock.Object, proxyTelemetryContext, CancellationToken.None, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
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
                ReadStream = readStream ?? throw new ArgumentNullException(nameof(readStream));
                WriteStream = writeStream ?? throw new ArgumentNullException(nameof(writeStream));
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
                this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
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

        private class TestTrailersFeature : IHttpResponseTrailersFeature
        {
            public IHeaderDictionary Trailers { get; set; } = new HeaderDictionary();
        }
    }
}
