// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
{
    public class HttpProxyTests
    {
        private IHttpProxy CreateProxy()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpProxy();
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IHttpProxy>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Assert.NotNull(CreateProxy());
        }

        // Tests normal (as opposed to upgradeable) request proxying.
        [Fact]
        public async Task ProxyAsync_NormalRequest_Works()
        {
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
            var sut = CreateProxy();
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

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

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
            var sut = CreateProxy();
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

            var proxyOptions = new RequestProxyOptions()
            {
                Transforms = transforms,
            };

            await sut.ProxyAsync(httpContext, destinationPrefix, client, proxyOptions);

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
            var sut = CreateProxy();
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

            var proxyOptions = new RequestProxyOptions()
            {
                Transforms = transforms,
            };

            await sut.ProxyAsync(httpContext, destinationPrefix, client, proxyOptions);

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
            var sut = CreateProxy();
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

                    Assert.Null(request.Content);

                    var response = new HttpResponseMessage((HttpStatusCode)234);
                    return response;
                });

            var proxyOptions = new RequestProxyOptions()
            {
                Transforms = transforms,
            };

            await sut.ProxyAsync(httpContext, destinationPrefix, client, proxyOptions);

            Assert.Equal(234, httpContext.Response.StatusCode);
        }

        // Tests proxying an upgradeable request.
        [Fact]
        public async Task ProxyAsync_UpgradableRequest_Works()
        {
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
            var sut = CreateProxy();
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

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

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

        // Tests proxying an upgradeable request where the destination refused to upgrade.
        // We should still proxy back the response.
        [Fact]
        public async Task ProxyAsync_UpgradableRequestFailsToUpgrade_ProxiesResponse()
        {
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
            var sut = CreateProxy();
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

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

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
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    Assert.Equal(new Version(2, 0), request.Version);
                    Assert.Equal(method, request.Method.Method, StringComparer.OrdinalIgnoreCase);

                    Assert.Null(request.Content);

                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                    return Task.FromResult(response);
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

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
            var sut = CreateProxy();
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

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task ProxyAsync_UnableToConnect_Returns502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.Request, errorFeature.Error);
            Assert.IsType<HttpRequestException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_UnableToConnectWithBody_Returns502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new MemoryStream(new byte[1]);
            httpContext.Request.ContentLength = 1;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.Request, errorFeature.Error);
            Assert.IsType<HttpRequestException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestTimedOut_Returns504()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    cancellationToken.WaitHandle.WaitOne();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage());
                });

            var proxyOptions = new RequestProxyOptions()
            {
                RequestTimeout = TimeSpan.FromTicks(1), // Time out immediately
            };

            await sut.ProxyAsync(httpContext, destinationPrefix, client, proxyOptions);

            Assert.Equal(StatusCodes.Status504GatewayTimeout, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestTimedOut, errorFeature.Error);
            Assert.IsType<OperationCanceledException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestCanceled_Returns502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.RequestAborted = new CancellationToken(canceled: true);

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage());
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestCanceled, errorFeature.Error);
            Assert.IsType<OperationCanceledException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestWithBodyTimedOut_Returns504()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new MemoryStream(new byte[1]);
            httpContext.Request.ContentLength = 1;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    cancellationToken.WaitHandle.WaitOne();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage());
                });

            var proxyOptions = new RequestProxyOptions()
            {
                RequestTimeout = TimeSpan.FromTicks(1), // Time out immediately
            };

            await sut.ProxyAsync(httpContext, destinationPrefix, client, proxyOptions);

            Assert.Equal(StatusCodes.Status504GatewayTimeout, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestTimedOut, errorFeature.Error);
            Assert.IsType<OperationCanceledException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestWithBodyCanceled_Returns502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new MemoryStream(new byte[1]);
            httpContext.Request.ContentLength = 1;
            httpContext.RequestAborted = new CancellationToken(canceled: true);

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage());
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestCanceled, errorFeature.Error);
            Assert.IsType<OperationCanceledException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestBodyClientErrorBeforeResponseError_Returns400()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new ThrowStream(throwOnFirstRead: true);
            httpContext.Request.ContentLength = 1;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    // Should throw.
                    await request.Content.CopyToAsync(Stream.Null);
                    return new HttpResponseMessage();
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestBodyClient, errorFeature.Error);
            Assert.IsType<AggregateException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestBodyDestinationErrorBeforeResponseError_Returns502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new MemoryStream(new byte[1]);
            httpContext.Request.ContentLength = 1;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    // Doesn't throw for destination errors
                    await request.Content.CopyToAsync(new ThrowStream());
                    throw new HttpRequestException();
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestBodyDestination, errorFeature.Error);
            Assert.IsType<AggregateException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestBodyCanceledBeforeResponseError_Returns502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new MemoryStream(new byte[1]);
            httpContext.Request.ContentLength = 1;
            httpContext.RequestAborted = new CancellationToken(canceled: true);

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    // should throw
                    try
                    {
                        await request.Content.CopyToAsync(new MemoryStream());
                    }
                    catch (OperationCanceledException) { }
                    throw new HttpRequestException();
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestBodyCanceled, errorFeature.Error);
            Assert.IsType<AggregateException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_ResponseBodyDestionationErrorFirstRead_Returns502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    var message = new HttpResponseMessage()
                    {
                        Content = new StreamContent(new ThrowStream(throwOnFirstRead: true))
                    };
                    message.Headers.AcceptRanges.Add("bytes");
                    return Task.FromResult(message);
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            Assert.Empty(httpContext.Response.Headers);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.ResponseBodyDestination, errorFeature.Error);
            Assert.IsType<IOException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_ResponseBodyDestionationErrorSecondRead_Aborted()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");
            var responseBody = new TestResponseBody();
            httpContext.Features.Set<IHttpResponseFeature>(responseBody);
            httpContext.Features.Set<IHttpResponseBodyFeature>(responseBody);
            httpContext.Features.Set<IHttpRequestLifetimeFeature>(responseBody);

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    var message = new HttpResponseMessage()
                    {
                        Content = new StreamContent(new ThrowStream(throwOnFirstRead: false))
                    };
                    message.Headers.AcceptRanges.Add("bytes");
                    return Task.FromResult(message);
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.Equal(1, responseBody.InnerStream.Length);
            Assert.True(responseBody.Aborted);
            Assert.Equal("bytes", httpContext.Response.Headers[HeaderNames.AcceptRanges]);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.ResponseBodyDestination, errorFeature.Error);
            Assert.IsType<IOException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_ResponseBodyClientError_Aborted()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");
            var responseBody = new TestResponseBody() { InnerStream = new ThrowStream() };
            httpContext.Features.Set<IHttpResponseFeature>(responseBody);
            httpContext.Features.Set<IHttpResponseBodyFeature>(responseBody);
            httpContext.Features.Set<IHttpRequestLifetimeFeature>(responseBody);

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    var message = new HttpResponseMessage()
                    {
                        Content = new StreamContent(new MemoryStream(new byte[1]))
                    };
                    message.Headers.AcceptRanges.Add("bytes");
                    return Task.FromResult(message);
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.True(responseBody.Aborted);
            Assert.Equal("bytes", httpContext.Response.Headers[HeaderNames.AcceptRanges]);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.ResponseBodyClient, errorFeature.Error);
            Assert.IsType<IOException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_ResponseBodyCancelled_502()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");
            var responseBody = new TestResponseBody();
            httpContext.Features.Set<IHttpResponseFeature>(responseBody);
            httpContext.Features.Set<IHttpResponseBodyFeature>(responseBody);
            httpContext.Features.Set<IHttpRequestLifetimeFeature>(responseBody);
            httpContext.RequestAborted = new CancellationToken(canceled: true);

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    var message = new HttpResponseMessage()
                    {
                        Content = new StreamContent(new MemoryStream(new byte[1]))
                    };
                    message.Headers.AcceptRanges.Add("bytes");
                    return Task.FromResult(message);
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
            Assert.False(responseBody.Aborted);
            Assert.Empty(httpContext.Response.Headers);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.ResponseBodyCanceled, errorFeature.Error);
            Assert.IsType<OperationCanceledException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_ResponseBodyCancelledAfterStart_Aborted()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Host = new HostString("example.com:3456");
            var responseBody = new TestResponseBody() { HasStarted = true };
            httpContext.Features.Set<IHttpResponseFeature>(responseBody);
            httpContext.Features.Set<IHttpResponseBodyFeature>(responseBody);
            httpContext.Features.Set<IHttpRequestLifetimeFeature>(responseBody);
            httpContext.RequestAborted = new CancellationToken(canceled: true);

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    var message = new HttpResponseMessage()
                    {
                        Content = new StreamContent(new MemoryStream(new byte[1]))
                    };
                    message.Headers.AcceptRanges.Add("bytes");
                    return Task.FromResult(message);
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.True(responseBody.Aborted);
            Assert.Equal("bytes", httpContext.Response.Headers[HeaderNames.AcceptRanges]);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.ResponseBodyCanceled, errorFeature.Error);
            Assert.IsType<OperationCanceledException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestBodyCanceledAfterResponse_Reported()
        {
            var waitTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new StallStream(waitTcs.Task);
            httpContext.Request.ContentLength = 1;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            using var longTokenSource = new CancellationTokenSource();
            httpContext.RequestAborted = longTokenSource.Token;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    // Background copy
                    _ = request.Content.CopyToAsync(new MemoryStream());
                    // Make sure the request isn't canceled until the response finishes copying.
                    return Task.FromResult(new HttpResponseMessage()
                    {
                        Content = new StreamContent(new OnCompletedReadStream(() =>
                        {
                            longTokenSource.Cancel();
                            waitTcs.SetResult(0);
                        }))
                    });
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestBodyCanceled, errorFeature.Error);
            Assert.IsType<OperationCanceledException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestBodyClientErrorAfterResponse_Reported()
        {
            var waitTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new StallStream(waitTcs.Task);
            httpContext.Request.ContentLength = 1;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    // Background copy
                    _ = request.Content.CopyToAsync(new MemoryStream());
                    // Make sure the request isn't canceled until the response finishes copying.
                    return Task.FromResult(new HttpResponseMessage()
                    {
                        Content = new StreamContent(new OnCompletedReadStream(() => waitTcs.SetResult(0)))
                    });
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestBodyClient, errorFeature.Error);
            Assert.IsType<IOException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_RequestBodyDestinationErrorAfterResponse_Reported()
        {
            var waitTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "POST";
            httpContext.Request.Host = new HostString("example.com:3456");
            httpContext.Request.Body = new MemoryStream(new byte[1]);
            httpContext.Request.ContentLength = 1;

            var proxyResponseStream = new MemoryStream();
            httpContext.Response.Body = proxyResponseStream;

            var destinationPrefix = "https://localhost:123/";
            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    // Background copy
                    _ = request.Content.CopyToAsync(new StallStream(waitTcs.Task));
                    // Make sure the request isn't canceled until the response finishes copying.
                    return Task.FromResult(new HttpResponseMessage()
                    {
                        Content = new StreamContent(new OnCompletedReadStream(() => waitTcs.SetResult(0)))
                    });
                });

            await sut.ProxyAsync(httpContext, destinationPrefix, client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.Equal(0, proxyResponseStream.Length);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.RequestBodyDestination, errorFeature.Error);
            Assert.IsType<IOException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_UpgradableRequest_RequestBodyCopyError_CancelsResponseBody()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("example.com:3456");
            // TODO: https://github.com/microsoft/reverse-proxy/issues/255
            httpContext.Request.Headers.Add("Upgrade", "WebSocket");

            var downstreamStream = new DuplexStream(
                readStream: new ThrowStream(),
                writeStream: new MemoryStream());
            DuplexStream upstreamStream = null;

            var upgradeFeatureMock = new Mock<IHttpUpgradeFeature>();
            upgradeFeatureMock.SetupGet(u => u.IsUpgradableRequest).Returns(true);
            upgradeFeatureMock.Setup(u => u.UpgradeAsync()).ReturnsAsync(downstreamStream);
            httpContext.Features.Set(upgradeFeatureMock.Object);

            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    Assert.Equal(new Version(1, 1), request.Version);
                    Assert.Equal(HttpMethod.Get, request.Method);

                    Assert.Null(request.Content);

                    var response = new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
                    upstreamStream = new DuplexStream(
                        readStream: new StallStream(ct =>
                        {
                            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                            ct.Register(() => tcs.SetResult(0));
                            return tcs.Task.DefaultTimeout();
                        }),
                        writeStream: new MemoryStream());
                    response.Content = new RawStreamContent(upstreamStream);
                    return Task.FromResult(response);
                });

            await sut.ProxyAsync(httpContext, "https://localhost/", client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status101SwitchingProtocols, httpContext.Response.StatusCode);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.UpgradeRequestClient, errorFeature.Error);
            Assert.IsType<IOException>(errorFeature.Exception);
        }

        [Fact]
        public async Task ProxyAsync_UpgradableRequest_ResponseBodyCopyError_CancelsRequestBody()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("example.com:3456");
            // TODO: https://github.com/microsoft/reverse-proxy/issues/255
            httpContext.Request.Headers.Add("Upgrade", "WebSocket");

            var downstreamStream = new DuplexStream(
                readStream: new StallStream(ct =>
                {
                    var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                    ct.Register(() => tcs.SetResult(0));
                    return tcs.Task.DefaultTimeout();
                }),
                writeStream: new MemoryStream());
            DuplexStream upstreamStream = null;

            var upgradeFeatureMock = new Mock<IHttpUpgradeFeature>();
            upgradeFeatureMock.SetupGet(u => u.IsUpgradableRequest).Returns(true);
            upgradeFeatureMock.Setup(u => u.UpgradeAsync()).ReturnsAsync(downstreamStream);
            httpContext.Features.Set(upgradeFeatureMock.Object);

            var sut = CreateProxy();
            var client = MockHttpHandler.CreateClient(
                (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    Assert.Equal(new Version(1, 1), request.Version);
                    Assert.Equal(HttpMethod.Get, request.Method);

                    Assert.Null(request.Content);

                    var response = new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
                    upstreamStream = new DuplexStream(
                        readStream: new ThrowStream(),
                        writeStream: new MemoryStream());
                    response.Content = new RawStreamContent(upstreamStream);
                    return Task.FromResult(response);
                });

            await sut.ProxyAsync(httpContext, "https://localhost/", client, new RequestProxyOptions());

            Assert.Equal(StatusCodes.Status101SwitchingProtocols, httpContext.Response.StatusCode);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.UpgradeResponseDestination, errorFeature.Error);
            Assert.IsType<IOException>(errorFeature.Exception);
        }

        private static MemoryStream StringToStream(string text)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            return stream;
        }

        private static string StreamToString(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            return reader.ReadToEnd();
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
            public DuplexStream(Stream readStream, Stream writeStream)
            {
                ReadStream = readStream ?? throw new ArgumentNullException(nameof(readStream));
                WriteStream = writeStream ?? throw new ArgumentNullException(nameof(writeStream));
            }

            public Stream ReadStream { get; }

            public Stream WriteStream { get; }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotImplementedException();

            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadStream.Read(buffer, offset, count);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return ReadStream.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return ReadStream.ReadAsync(buffer, cancellationToken);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                WriteStream.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return WriteStream.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return WriteStream.WriteAsync(buffer, cancellationToken);
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

        private class ThrowStream : Stream
        {
            private bool _firstRead = true;

            public ThrowStream(bool throwOnFirstRead = true)
            {
                ThrowOnFirstRead = throwOnFirstRead;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public bool ThrowOnFirstRead { get; }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                // If we want this to throw then make it conditional. Write is more interesting.
                return Task.CompletedTask;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_firstRead && !ThrowOnFirstRead)
                {
                    _firstRead = false;
                    return Task.FromResult(1);
                }
                throw new IOException("Fake connection issue");
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException("Fake connection issue");
            }
        }

        private class StallStream : Stream
        {
            public StallStream(Task until) : this(_ => until) 
            {
            }

            public StallStream(Func<CancellationToken, Task> onStallAction)
            {
                OnStallAction = onStallAction;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public Func<CancellationToken, Task> OnStallAction { get; }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                // If we want this to throw then make it conditional. Write is more interesting.
                return Task.CompletedTask;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await OnStallAction(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await OnStallAction(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException();
            }
        }

        private class TestResponseBody : Stream, IHttpResponseBodyFeature, IHttpResponseFeature, IHttpRequestLifetimeFeature
        {
            public Stream InnerStream { get; set; } = new MemoryStream();

            public bool Aborted { get; private set; }

            public Stream Stream => this;

            public PipeWriter Writer => throw new NotImplementedException();

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public int StatusCode { get; set; } = 200;
            public string ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get => this; set => throw new NotImplementedException(); }
            public bool HasStarted { get; set; }
            public CancellationToken RequestAborted { get; set; }

            public void Abort()
            {
                Aborted = true;
            }

            public Task CompleteAsync()
            {
                throw new NotImplementedException();
            }

            public void DisableBuffering()
            {
                throw new NotImplementedException();
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public void OnCompleted(Func<object, Task> callback, object state)
            {
                throw new NotImplementedException();
            }

            public void OnStarting(Func<object, Task> callback, object state)
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                OnStart();
                return InnerStream.WriteAsync(buffer, offset, count, cancellationToken);
            }

            private void OnStart()
            {
                if (!HasStarted)
                {
                    HasStarted = true;
                }
            }
        }

        private class OnCompletedReadStream : Stream
        {
            public OnCompletedReadStream(Action onCompleted)
            {
                OnCompleted = onCompleted;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public Action OnCompleted { get; }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                // If we want this to throw then make it conditional. Write is more interesting.
                return Task.CompletedTask;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                OnCompleted();
                return Task.FromResult(0);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
