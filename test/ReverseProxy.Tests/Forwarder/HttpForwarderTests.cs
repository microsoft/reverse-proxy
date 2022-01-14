// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
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
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Utilities;
using System.Net.Http.Headers;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class HttpForwarderTests
{
    private IHttpForwarder CreateProxy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpForwarder();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpForwarder>();
    }

    [Fact]
    public void Constructor_Works()
    {
        Assert.NotNull(CreateProxy());
    }

    // Tests normal (as opposed to upgradeable) request proxying.
    [Fact]
    public async Task NormalRequest_Works()
    {
        var events = TestEventListener.Collect();

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

        var requestBody = "request content";
        httpContext.Request.Headers.Add("Content-Length", requestBody.Length.ToString());
        httpContext.Request.Body = StringToStream(requestBody);
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
                Assert.Null(request.Headers.Host);
                Assert.False(request.Headers.TryGetValues(":authority", out var value));

                Assert.NotNull(request.Content);
                Assert.Contains("requestLanguage", request.Content.Headers.GetValues("Content-Language"));

                var capturedRequestContent = new MemoryStream();

                // Use CopyToAsync as this is what HttpClient and friends use internally
                await request.Content.CopyToWithCancellationAsync(capturedRequestContent);
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

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(234, httpContext.Response.StatusCode);
        var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
        Assert.Equal("Test Reason Phrase", reasonPhrase);
        Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());
        Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());

        proxyResponseStream.Position = 0;
        var proxyResponseText = StreamToString(proxyResponseStream);
        Assert.Equal("response content", proxyResponseText);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages();
    }

    [Fact]
    public async Task NormalRequestWithTransforms_Works()
    {
        var events = TestEventListener.Collect();

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
        var transforms = new DelegateHttpTransforms()
        {
            OnRequest = (context, request, destination) =>
            {
                request.RequestUri = new Uri(destination + "prefix"
                    + context.Request.Path + context.Request.QueryString);
                request.Headers.Remove("transformHeader");
                request.Headers.TryAddWithoutValidation("transformHeader", "value");
                request.Headers.TryAddWithoutValidation("x-ms-request-test", "transformValue");
                request.Headers.Host = null;
                return Task.CompletedTask;
            },
            OnResponse = (context, response) =>
            {
                context.Response.Headers["transformHeader"] = "value";
                context.Response.Headers.Append("x-ms-response-test", "value");
                return new(true);
            },
            OnResponseTrailers = (context, response) =>
            {
                context.Response.AppendTrailer("trailerTransform", "value");
                return Task.CompletedTask;
            }
        };

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
                await request.Content.CopyToWithCancellationAsync(capturedRequestContent);
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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, ForwarderRequestConfig.Empty, transforms);

        Assert.Equal(ForwarderError.None, proxyError);
        Assert.Equal(234, httpContext.Response.StatusCode);
        var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
        Assert.Null(reasonPhrase); // We don't set the ReasonPhrase for HTTP/2+
        Assert.Equal(new[] { "response", "value" }, httpContext.Response.Headers["x-ms-response-test"].ToArray());
        Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());
        Assert.Contains("value", httpContext.Response.Headers["transformHeader"].ToArray());
        Assert.Equal(new[] { "value" }, httpContext.Features.Get<IHttpResponseTrailersFeature>().Trailers?["trailerTransform"].ToArray());

        proxyResponseStream.Position = 0;
        var proxyResponseText = StreamToString(proxyResponseStream);
        Assert.Equal("response content", proxyResponseText);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages();
    }

    [Fact]
    public async Task NormalRequestWithCopyRequestHeadersDisabled_Works()
    {
        var events = TestEventListener.Collect();

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
        var transforms = new DelegateHttpTransforms()
        {
            CopyRequestHeaders = false,
            OnRequest = (context, request, destination) =>
            {
                request.Headers.TryAddWithoutValidation("x-ms-request-test", "transformValue");
                return Task.CompletedTask;
            }
        };
        var targetUri = "https://localhost:123/a/b/test?a=b&c=d";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                Assert.Equal(new Version(2, 0), request.Version);
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(targetUri, request.RequestUri.AbsoluteUri);
                Assert.Equal(new[] { "transformValue" }, request.Headers.GetValues("x-ms-request-test"));
                Assert.Null(request.Headers.Host);
                Assert.False(request.Headers.TryGetValues(":authority", out var _));

                Assert.NotNull(request.Content);
                Assert.False(request.Content.Headers.TryGetValues("Content-Language", out var _));

                var capturedRequestContent = new MemoryStream();

                // Use CopyToAsync as this is what HttpClient and friends use internally
                await request.Content.CopyToWithCancellationAsync(capturedRequestContent);
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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, ForwarderRequestConfig.Empty, transforms);

        Assert.Equal(ForwarderError.None, proxyError);
        Assert.Equal(234, httpContext.Response.StatusCode);
        var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
        Assert.Equal("Test Reason Phrase", reasonPhrase);
        Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());
        Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());

        proxyResponseStream.Position = 0;
        var proxyResponseText = StreamToString(proxyResponseStream);
        Assert.Equal("response content", proxyResponseText);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages();
    }

    // Tests proxying an upgradeable request.
    [Theory]
    [InlineData("WebSocket")]
    [InlineData("SPDY/3.1")]
    public async Task UpgradableRequest_Works(string upgradeHeader)
    {
        var events = TestEventListener.Collect();

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
        // https://github.com/microsoft/reverse-proxy/issues/467
        httpContext.Request.Headers.Add("Upgrade", upgradeHeader);

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
                Assert.Null(request.Headers.Host);
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

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(StatusCodes.Status101SwitchingProtocols, httpContext.Response.StatusCode);
        Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());

        downstreamStream.WriteStream.Position = 0;
        var returnedToDownstream = StreamToString(downstreamStream.WriteStream);
        Assert.Equal("response content", returnedToDownstream);

        Assert.NotNull(upstreamStream);
        upstreamStream.WriteStream.Position = 0;
        var sentToUpstream = StreamToString(upstreamStream.WriteStream);
        Assert.Equal("request content", sentToUpstream);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(upgrade: true);
    }

    [Fact]
    public async Task NonUpgradableRequestReturns101_Aborted()
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        DuplexStream upstreamStream = null;

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                Assert.Equal(new Version(2, 0), request.Version);
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Null(request.Content);

                var response = new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
                upstreamStream = new DuplexStream(
                    readStream: StringToStream("response content"),
                    writeStream: new MemoryStream());
                response.Content = new RawStreamContent(upstreamStream);
                return response;
            });

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, ForwarderError.UpgradeResponseDestination);
        events.AssertContainProxyStages(upgrade: true, hasRequestContent: false, hasResponseContent: false);
    }

    // Tests proxying an upgradeable request where the destination refused to upgrade.
    // We should still proxy back the response.
    [Fact]
    public async Task UpgradableRequestFailsToUpgrade_ProxiesResponse()
    {
        var events = TestEventListener.Collect();

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

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(234, httpContext.Response.StatusCode);
        var reasonPhrase = httpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
        Assert.Equal("Test Reason Phrase", reasonPhrase);
        Assert.Contains("response", httpContext.Response.Headers["x-ms-response-test"].ToArray());
        Assert.Contains("responseLanguage", httpContext.Response.Headers["Content-Language"].ToArray());

        proxyResponseStream.Position = 0;
        var proxyResponseText = StreamToString(proxyResponseStream);
        Assert.Equal("response content", proxyResponseText);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false, upgrade: false);
    }

    [Fact]
    public async Task UpgradableRequest_CancelsIfIdle()
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Scheme = "http";
        httpContext.Request.Path = "/api/test";
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

        // TODO: https://github.com/microsoft/reverse-proxy/issues/255
        // https://github.com/microsoft/reverse-proxy/issues/467
        httpContext.Request.Headers.Add("Upgrade", "WebSocket");

        var _idleTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var downstreamStream = new DuplexStream(
            readStream: new StallStream(ct =>
            {
                ct.Register(() => _idleTcs.TrySetCanceled());
                return _idleTcs.Task;
            }),
            writeStream: new MemoryStream());
        DuplexStream upstreamStream = null;

        var upgradeFeatureMock = new Mock<IHttpUpgradeFeature>();
        upgradeFeatureMock.SetupGet(u => u.IsUpgradableRequest).Returns(true);
        upgradeFeatureMock.Setup(u => u.UpgradeAsync()).ReturnsAsync(downstreamStream);
        httpContext.Features.Set(upgradeFeatureMock.Object);

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                Assert.Equal(new Version(1, 1), request.Version);
                Assert.Equal(HttpMethod.Get, request.Method);

                Assert.Null(request.Content);

                var response = new HttpResponseMessage(HttpStatusCode.SwitchingProtocols);
                upstreamStream = new DuplexStream(
                    readStream: new StallStream(ct =>
                    {
                        ct.Register(() => _idleTcs.TrySetCanceled());
                        return _idleTcs.Task;
                    }),
                    writeStream: new MemoryStream());
                response.Content = new RawStreamContent(upstreamStream);
                return response;
            });

        var result = await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromSeconds(1)
        }).DefaultTimeout();

        Assert.Equal(StatusCodes.Status101SwitchingProtocols, httpContext.Response.StatusCode);

        // When both are idle it's a race which gets reported as canceled first.
        Assert.True(ForwarderError.UpgradeRequestCanceled == result
            || ForwarderError.UpgradeResponseCanceled == result);

        events.AssertContainProxyStages(upgrade: true);
    }

    [Theory]
    [InlineData("TRACE", "HTTP/1.1", "")]
    [InlineData("TRACE", "HTTP/2", "")]
    [InlineData("GET", "HTTP/1.1", "")]
    [InlineData("GET", "HTTP/2", "")]
    [InlineData("GET", "HTTP/1.1", "Content-Length:0")]
    [InlineData("HEAD", "HTTP/1.1", "")]
    [InlineData("POST", "HTTP/1.1", "")]
    [InlineData("POST", "HTTP/1.1", "Content-Length:0")]
    [InlineData("POST", "HTTP/1.1", "Content-Length:0;Content-Type:text/plain")]
    [InlineData("POST", "HTTP/2", "Content-Length:0")]
    [InlineData("POST", "HTTP/2", "Content-Length:0;Content-Type:text/plain")]
    [InlineData("PATCH", "HTTP/1.1", "")]
    [InlineData("DELETE", "HTTP/1.1", "")]
    [InlineData("Unknown", "HTTP/1.1", "")]
    // [InlineData("CONNECT", "HTTP/1.1", "")] Blocked in HttpUtilities.GetHttpMethod
    [InlineData("GET", "HTTP/1.1", "Allow:Foo")]
    [InlineData("GET", "HTTP/1.1", "Content-Disposition:Foo")]
    [InlineData("GET", "HTTP/1.1", "Content-Encoding:Foo")]
    [InlineData("GET", "HTTP/1.1", "Content-Language:Foo")]
    [InlineData("GET", "HTTP/1.1", "Content-Location:Foo")]
    [InlineData("GET", "HTTP/1.1", "Content-MD5:Foo")]
    [InlineData("GET", "HTTP/1.1", "Content-Range:Foo")]
    [InlineData("GET", "HTTP/1.1", "Content-Type:Foo")]
    [InlineData("GET", "HTTP/1.1", "Expires:Foo")]
    [InlineData("GET", "HTTP/1.1", "Last-Modified:Foo")]
    public async Task RequestWithoutBodies_NoHttpContent(string method, string protocol, string headerList)
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Protocol = protocol;

        var headers = headerList
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(header => (Key: header.Split(':')[0], Value: header.Split(':')[1]))
            .ToArray();

        foreach (var (key, value) in headers)
        {
            httpContext.Request.Headers[key] = value;
        }

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(2, 0), request.Version);
                Assert.Equal(method, request.Method.Method, StringComparer.OrdinalIgnoreCase);

                // When adding content-specific headers, we will inject an EmptyHttpContent if no other content is present
                if (headers.Any())
                {
                    Assert.NotNull(request.Content);
                    Assert.IsType<EmptyHttpContent>(request.Content);
                    Assert.Empty(await request.Content.ReadAsByteArrayAsync());

                    foreach (var (key, value) in headers)
                    {
                        Assert.True(request.Content.Headers.TryGetValues(key, out var values));
                        Assert.Equal(value, Assert.Single(values));
                    }

                    // If a custom content is injected, so is a "Content-Length: 0" header
                    Assert.True(request.Content.Headers.TryGetValues(HeaderNames.ContentLength, out var contentLength));
                    Assert.Equal("0", Assert.Single(contentLength));
                }
                else
                {
                    Assert.Null(request.Content);
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            });

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Theory]
    [InlineData("POST", "HTTP/2", "", "")]
    [InlineData("PATCH", "HTTP/2", "", "")]
    [InlineData("UNKNOWN", "HTTP/2", "", "")]
    [InlineData("UNKNOWN", "HTTP/1.1", "Content-Length:10", "aaaaaaaaaa")]
    [InlineData("UNKNOWN", "HTTP/1.1", "transfer-encoding:Chunked", "")]
    [InlineData("GET", "HTTP/1.1", "Content-Length:10", "aaaaaaaaaa")]
    [InlineData("GET", "HTTP/2", "Content-Length:10", "aaaaaaaaaa")]
    [InlineData("HEAD", "HTTP/1.1", "transfer-encoding:Chunked", "")]
    [InlineData("HEAD", "HTTP/2", "transfer-encoding:Chunked", "")]
    public async Task RequestWithBodies_HasHttpContent(string method, string protocol, string headers, string body)
    {
        var events = TestEventListener.Collect();

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
        httpContext.Request.Body = StringToStream(body);

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(2, 0), request.Version);
                Assert.Equal(method, request.Method.Method, StringComparer.OrdinalIgnoreCase);

                Assert.NotNull(request.Content);

                // Must consume the body
                await request.Content.CopyToWithCancellationAsync(Stream.Null);

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            });

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages();
    }

    [Theory]
    [InlineData("1.1", 1, "")]
    [InlineData("1.1", 1, "aa")]
    [InlineData("1.1", 2, "a")]
    [InlineData("2.0", 1, "")]
    [InlineData("2.0", 1, "aa")]
    [InlineData("2.0", 2, "a")]
    public async Task RequestWithBodies_WrongContentLength(string version, long contentLength, string body)
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.ContentLength = contentLength;
        httpContext.Request.Body = StringToStream(body);

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(version), request.Version);

                Assert.NotNull(request.Content);

                // Must throw
                try
                {
                    await request.Content.CopyToWithCancellationAsync(Stream.Null);
                }
                catch (HttpRequestException ex)
                {
                    Assert.Contains("Content-Length", ex.InnerException.InnerException.Message);
                    throw ex;
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            });

        var options = new ForwarderRequestConfig
        {
            Version = new Version(version),
        };

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, options);

        Assert.Equal(ForwarderError.RequestBodyClient, proxyError);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestBodyClient, errorFeature.Error);
        Assert.IsType<AggregateException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart, ForwarderStage.RequestContentTransferStart });
    }

    [Fact]
    public async Task RequestWithBodies_WithoutContentLength()
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Protocol = "HTTP/2";
        httpContext.Request.Body = StringToStream("request content");

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(2, 0), request.Version);

                Assert.NotNull(request.Content);

                // Must consume the body
                await request.Content.CopyToWithCancellationAsync(Stream.Null);

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            });

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages();
    }

#if NET
    [Fact]
    public async Task BodyDetectionFeatureSaysNo_NoHttpContent()
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Features.Set<IHttpRequestBodyDetectionFeature>(new TestBodyDetector() { CanHaveBody = false });

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(2, 0), request.Version);

                Assert.Null(request.Content);

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                return Task.FromResult(response);
            });

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Fact]
    public async Task BodyDetectionFeatureSaysYes_HasHttpContent()
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Features.Set<IHttpRequestBodyDetectionFeature>(new TestBodyDetector() { CanHaveBody = true });

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(2, 0), request.Version);

                Assert.NotNull(request.Content);

                // Must consume the body
                await request.Content.CopyToWithCancellationAsync(Stream.Null);

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            });

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages();
    }

    private class TestBodyDetector : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; set; }
    }
#endif

    [Theory]
    // This is an invalid format per spec but may happen due to https://github.com/dotnet/aspnetcore/issues/26461
    [InlineData("testA=A_Cookie", "testB=B_Cookie", "testC=C_Cookie")]
    [InlineData("testA=A_Value", "testB=B_Value", "testC=C_Value")]
    [InlineData("testA=A_Value, testB=B_Value", "testC=C_Value")]
    [InlineData("testA=A_Value", "", "testB=B_Value, testC=C_Value")]
    [InlineData("testA=A_Value, testB=B_Value, testC=C_Value")]
    [InlineData("", "")]
    public async Task RequestWithCookieHeaders(params string[] cookies)
    {
        var events = TestEventListener.Collect();

        
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers.Add(HeaderNames.Cookie, cookies);

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                // "testA=A_Cookie; testB=B_Cookie; testC=C_Cookie"
                var expectedCookieString = string.Join("; ", cookies);

                Assert.Equal(new Version(2, 0), request.Version);
                Assert.Equal("GET", request.Method.Method, StringComparer.OrdinalIgnoreCase);
                Assert.Null(request.Content);
                Assert.True(request.Headers.TryGetValues(HeaderNames.Cookie, out var cookieHeaders));
                Assert.NotNull(cookieHeaders);
                var cookie = Assert.Single(cookieHeaders);
                Assert.Equal(expectedCookieString, cookie);

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                return Task.FromResult(response);
            });

        await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Null(httpContext.Features.Get<IForwarderErrorFeature>());
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Theory]
    [MemberData(nameof(RequestMultiHeadersData))]
    public async Task RequestWithMultiHeaders(string version, string headerName, string[] headers)
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers.Add(headerName, headers);

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(version), request.Version);
                Assert.Equal("GET", request.Method.Method, StringComparer.OrdinalIgnoreCase);
                IEnumerable<string> sentHeaders;
                if (headerName.StartsWith("Content"))
                {
                    Assert.True(request.Content.Headers.TryGetValues(headerName, out sentHeaders));
                }
                else
                {
                    Assert.True(request.Headers.TryGetValues(headerName, out sentHeaders));
                }

                Assert.NotNull(sentHeaders);
                Assert.True(AreEqualIgnoringEmptyStrings(sentHeaders, headers));

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                return Task.FromResult(response);
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = new Version(version) });

        Assert.Null(httpContext.Features.Get<IForwarderErrorFeature>());
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

#if NET6_0_OR_GREATER
    [Theory]
    [MemberData(nameof(RequestEmptyMultiHeadersData))]
    public async Task RequestWithEmptyMultiHeaders(string version, string headerName, string[] headers)
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers.Add(headerName, headers);

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(version), request.Version);
                Assert.Equal("GET", request.Method.Method, StringComparer.OrdinalIgnoreCase);
                HeaderStringValues sentHeaders;
                if (headerName.StartsWith("Content"))
                {
                    Assert.True(request.Content.Headers.NonValidated.TryGetValues(headerName, out sentHeaders));
                }
                else
                {
                    Assert.True(request.Headers.NonValidated.TryGetValues(headerName, out sentHeaders));
                }
                Assert.Equal(sentHeaders, headers);

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                return Task.FromResult(response);
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = new Version(version) });

        Assert.Null(httpContext.Features.Get<IForwarderErrorFeature>());
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false);
    }
#endif

    internal static bool AreEqualIgnoringEmptyStrings(IEnumerable<string> left, IEnumerable<string> right)
    => Enumerable.SequenceEqual(left.Where(s => !string.IsNullOrEmpty(s)).ToArray(), right.Where(s => !string.IsNullOrEmpty(s)).ToArray());

    public static IEnumerable<string> RequestMultiHeaderNames()
    {
        var headers = new[]
        {
            HeaderNames.Accept,
            HeaderNames.AcceptCharset,
            HeaderNames.AcceptEncoding,
            HeaderNames.AcceptLanguage,
            HeaderNames.ContentEncoding,
            HeaderNames.ContentLanguage,
            HeaderNames.ContentType,
            HeaderNames.Via
        };

        foreach (var header in headers)
        {
            yield return header;
        }
    }

    public static IEnumerable<string> ResponseMultiHeaderNames()
    {
        var headers = new[]
        {
            HeaderNames.AcceptRanges,
            HeaderNames.Allow,
            HeaderNames.ContentEncoding,
            HeaderNames.ContentLanguage,
            HeaderNames.ContentRange,
            HeaderNames.ContentType,
            HeaderNames.SetCookie,
            HeaderNames.Via,
            HeaderNames.Warning,
            HeaderNames.WWWAuthenticate
        };

        foreach (var header in headers)
        {
            yield return header;
        }
    }

    public static IEnumerable<string[]> MultiValues()
    {
        var values = new string[][] {
            new[] { "testA=A_Value", "testB=B_Value", "testC=C_Value" },
            new[] { "testA=A_Value, testB=B_Value", "testC=C_Value" },
            new[] { "testA=A_Value", "",  "testB=B_Value, testC=C_Value" },
            new[] { "testA=A_Value, testB=B_Value, testC=C_Value" }
        };

        foreach (var value in values)
        {
            yield return value;
        }
    }

    public static IEnumerable<object[]> RequestMultiHeadersData()
    {
        foreach (var header in RequestMultiHeaderNames())
        {
            foreach (var value in MultiValues())
            {
                foreach (var version in new[] { "1.1", "2.0" })
                {
                    yield return new object[] { version, header, value };
                }
            }
        }
    }

    public static IEnumerable<object[]> ResponseMultiHeadersData()
    {
        foreach (var header in ResponseMultiHeaderNames())
        {
            foreach (var version in new[] { "1.1", "2.0" })                
            {
                foreach (var value in MultiValues())
                {
                    yield return new object[] { version, header, value };
                }
                yield return new object[] { version, header, new[] { "", "" } };
            }
        }
    }

    public static IEnumerable<object[]> RequestEmptyMultiHeadersData()
    {
        foreach (var header in RequestMultiHeaderNames())
        {
            foreach (var version in new[] { "1.1", "2.0" })
            {
                yield return new object[] { version, header, new[] { "", "" } };
            }
        }
    }

    public static IEnumerable<object[]> ResponseEmptyMultiHeadersData()
    {
        foreach (var header in ResponseMultiHeaderNames())
        {
            foreach (var version in new[] { "1.1", "2.0" })
            {
                yield return new object[] { version, header, new[] { "", "" } };
            }
        }
    }

    [Fact]
    public async Task OptionsWithVersion()
    {
        var events = TestEventListener.Collect();

        // Use any non-default value
        var version = new Version(5, 5);
#if NET
        var versionPolicy = HttpVersionPolicy.RequestVersionExact;
#endif

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(version, request.Version);
#if NET
                Assert.Equal(versionPolicy, request.VersionPolicy);
#endif
                Assert.Equal("GET", request.Method.Method, StringComparer.OrdinalIgnoreCase);
                Assert.Null(request.Content);

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                return Task.FromResult(response);
            });

        var options = new ForwarderRequestConfig
        {
            Version = version,
#if NET
            VersionPolicy = versionPolicy,
#endif
        };

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, options);

        Assert.Equal(ForwarderError.None, proxyError);
        Assert.Null(httpContext.Features.Get<IForwarderErrorFeature>());
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Fact]
    public async Task OptionsWithVersion_Transformed()
    {
        var events = TestEventListener.Collect();

        // Use any non-default value
        var version = new Version(5, 5);
        var transformedVersion = new Version(6, 6);
#if NET
        var versionPolicy = HttpVersionPolicy.RequestVersionExact;
        var transformedVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
#endif

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        var destinationPrefix = "https://localhost/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(transformedVersion, request.Version);
#if NET
                Assert.Equal(transformedVersionPolicy, request.VersionPolicy);
#endif
                Assert.Equal("GET", request.Method.Method, StringComparer.OrdinalIgnoreCase);
                Assert.Null(request.Content);

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                return Task.FromResult(response);
            });

        var transforms = new DelegateHttpTransforms()
        {
            CopyRequestHeaders = false,
            OnRequest = (context, request, destination) =>
            {
                Assert.Equal(version, request.Version);
                request.Version = transformedVersion;
#if NET
                Assert.Equal(versionPolicy, request.VersionPolicy);
                request.VersionPolicy = transformedVersionPolicy;
#endif
                return Task.CompletedTask;
            }
        };

        var requestOptions = new ForwarderRequestConfig
        {
            Version = version,
#if NET
            VersionPolicy = versionPolicy,
#endif
        };

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, requestOptions, transforms);

        Assert.Equal(ForwarderError.None, proxyError);
        Assert.Null(httpContext.Features.Get<IForwarderErrorFeature>());
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Fact]
    public async Task UnableToConnect_Returns502()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.Request, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.Request, errorFeature.Error);
        Assert.IsType<HttpRequestException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart });
    }

    [Fact]
    public async Task UnableToConnectWithBody_Returns502()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.Request, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.Request, errorFeature.Error);
        Assert.IsType<HttpRequestException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart });
    }

    [Fact]
    public async Task RequestTimedOut_Returns504()
    {
        var events = TestEventListener.Collect();

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

        // Time out immediately
        var requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromTicks(1) };

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, requestOptions);

        Assert.Equal(ForwarderError.RequestTimedOut, proxyError);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestTimedOut, errorFeature.Error);
        Assert.IsType<OperationCanceledException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart });
    }

    [Fact]
    public async Task RequestCanceled_Returns502()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestCanceled, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestCanceled, errorFeature.Error);
        Assert.IsType<OperationCanceledException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart });
    }

    [Fact]
    public async Task RequestWithBodyTimedOut_Returns504()
    {
        var events = TestEventListener.Collect();

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

        // Time out immediately
        var requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromTicks(1) };

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, requestOptions);

        Assert.Equal(ForwarderError.RequestTimedOut, proxyError);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestTimedOut, errorFeature.Error);
        Assert.IsType<OperationCanceledException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart });
    }

    [Fact]
    public async Task RequestWithBody_KeptAliveByActivity()
    {
        var events = TestEventListener.Collect();

        var reads = 0;
        var expectedReads = 6;

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Body = new CallbackReadStream(async (memory, ct) =>
        {
            if (memory.Length == 0 || reads >= expectedReads)
            {
                return 0;
            }
            reads++;
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            memory.Span[0] = (byte)'a';
            return 1;
        });
        httpContext.Request.ContentLength = expectedReads;

        var proxyResponseStream = new MemoryStream();
        httpContext.Response.Body = proxyResponseStream;

        var destinationPrefix = "https://localhost:123/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                Assert.Equal(new Version(2, 0), request.Version);
                Assert.Equal("POST", request.Method.Method, StringComparer.OrdinalIgnoreCase);

                Assert.NotNull(request.Content);

                // Must consume the body
                var body = new MemoryStream();
                await request.Content.CopyToWithCancellationAsync(body);

                Assert.Equal(expectedReads, body.Length);

                cancellationToken.ThrowIfCancellationRequested();

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            });

        var requestOptions = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(1) };

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, requestOptions);

        Assert.Equal(ForwarderError.None, proxyError);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart, ForwarderStage.SendAsyncStop,
            ForwarderStage.RequestContentTransferStart, ForwarderStage.ResponseContentTransferStart,  });
    }

    [Fact]
    public async Task RequestWithBodyCanceled_Returns502()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestCanceled, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestCanceled, errorFeature.Error);
        Assert.IsType<OperationCanceledException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart });
    }

    [Fact]
    public async Task RequestBodyClientErrorBeforeResponseError_Returns400()
    {
        var events = TestEventListener.Collect();

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
                await request.Content.CopyToWithCancellationAsync(Stream.Null);
                return new HttpResponseMessage();
            });

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestBodyClient, proxyError);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestBodyClient, errorFeature.Error);
        Assert.IsType<AggregateException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] {
            ForwarderStage.SendAsyncStart,
            ForwarderStage.RequestContentTransferStart
        });
    }

    [Fact]
    public async Task RequestBodyDestinationErrorBeforeResponseError_Returns502()
    {
        var events = TestEventListener.Collect();

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
                await request.Content.CopyToWithCancellationAsync(new ThrowStream());
                throw new HttpRequestException();
            });

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestBodyDestination, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestBodyDestination, errorFeature.Error);
        Assert.IsType<AggregateException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] {
            ForwarderStage.SendAsyncStart,
            ForwarderStage.RequestContentTransferStart
        });
    }

    [Fact]
    public async Task RequestBodyCanceledBeforeResponseError_Returns502()
    {
        var events = TestEventListener.Collect();

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
                    await request.Content.CopyToWithCancellationAsync(new MemoryStream());
                }
                catch (OperationCanceledException) { }
                throw new HttpRequestException();
            });

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestBodyCanceled, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestBodyCanceled, errorFeature.Error);
        Assert.IsType<AggregateException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(new[] { ForwarderStage.SendAsyncStart });
    }

    [Fact]
    public async Task ResponseBodySuppressedByTransform_ReturnsStatusCodeAndHeaders()
    {
        var events = TestEventListener.Collect();

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
                    StatusCode = HttpStatusCode.UnprocessableEntity,
                    Content = new StreamContent(new ThrowStream(throwOnFirstRead: true))
                };
                message.Headers.AcceptRanges.Add("bytes");
                return Task.FromResult(message);
            });

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, ForwarderRequestConfig.Empty, new DelegateHttpTransforms()
        {
            OnResponse = (context, proxyResponse) =>
            {
                Assert.Equal(HttpStatusCode.UnprocessableEntity, proxyResponse.StatusCode);
                Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
                return new(false);
            }
        });

        Assert.Equal(ForwarderError.None, proxyError);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        Assert.Equal("bytes", httpContext.Response.Headers[HeaderNames.AcceptRanges]);
        Assert.Null(httpContext.Features.Get<IForwarderErrorFeature>());

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: false, hasResponseContent: false);
    }

    [Fact]
    public async Task ResponseBodyDestionationErrorFirstRead_Returns502()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.ResponseBodyDestination, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        Assert.Empty(httpContext.Response.Headers);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.ResponseBodyDestination, errorFeature.Error);
        Assert.IsType<IOException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Fact]
    public async Task ResponseBodyDestionationErrorSecondRead_Aborted()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.ResponseBodyDestination, proxyError);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal(1, responseBody.InnerStream.Length);
        Assert.True(responseBody.Aborted);
        Assert.Equal("bytes", httpContext.Response.Headers[HeaderNames.AcceptRanges]);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.ResponseBodyDestination, errorFeature.Error);
        Assert.IsType<IOException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Fact]
    public async Task ResponseBodyClientError_Aborted()
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Host = new HostString("example.com:3456");
        var responseBody = new TestResponseBody(new ThrowStream());
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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.ResponseBodyClient, proxyError);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.True(responseBody.Aborted);
        Assert.Equal("bytes", httpContext.Response.Headers[HeaderNames.AcceptRanges]);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.ResponseBodyClient, errorFeature.Error);
        Assert.IsType<IOException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Fact]
    public async Task ResponseBodyCancelled_502()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.ResponseBodyCanceled, proxyError);
        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.False(responseBody.Aborted);
        Assert.Empty(httpContext.Response.Headers);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.ResponseBodyCanceled, errorFeature.Error);
        Assert.IsType<TaskCanceledException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Fact]
    public async Task ResponseBodyCancelledAfterStart_Aborted()
    {
        var events = TestEventListener.Collect();

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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.ResponseBodyCanceled, proxyError);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.True(responseBody.Aborted);
        Assert.Equal("bytes", httpContext.Response.Headers[HeaderNames.AcceptRanges]);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.ResponseBodyCanceled, errorFeature.Error);
        Assert.IsType<TaskCanceledException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(hasRequestContent: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(null)]
    public async Task ResponseBodyDisableBuffering_Success(bool? enableBuffering)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
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
                    Content = new StreamContent(new MemoryStream(new byte[1]))
                };
                message.Headers.AcceptRanges.Add("bytes");
                return Task.FromResult(message);
            });

        var requestConfig = ForwarderRequestConfig.Empty with { AllowResponseBuffering = enableBuffering };
        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client, requestConfig, HttpTransformer.Default);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal(enableBuffering != true, responseBody.BufferingDisabled);
    }

    [Fact]
    public async Task RequestBodyCanceledAfterResponse_Reported()
    {
        var events = TestEventListener.Collect();

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
                _ = request.Content.CopyToWithCancellationAsync(new MemoryStream());
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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestBodyCanceled, proxyError);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestBodyCanceled, errorFeature.Error);
        Assert.IsType<OperationCanceledException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages();
    }

    [Fact]
    public async Task RequestBodyClientErrorAfterResponse_Reported()
    {
        var events = TestEventListener.Collect();

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
                _ = request.Content.CopyToWithCancellationAsync(new MemoryStream());
                // Make sure the request isn't canceled until the response finishes copying.
                return Task.FromResult(new HttpResponseMessage()
                {
                    Content = new StreamContent(new OnCompletedReadStream(() => waitTcs.SetResult(0)))
                });
            });

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestBodyClient, proxyError);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestBodyClient, errorFeature.Error);
        Assert.IsType<IOException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages();
    }

    [Fact]
    public async Task RequestBodyDestinationErrorAfterResponse_Reported()
    {
        var events = TestEventListener.Collect();

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
                _ = request.Content.CopyToWithCancellationAsync(new StallStream(waitTcs.Task));
                // Make sure the request isn't canceled until the response finishes copying.
                return Task.FromResult(new HttpResponseMessage()
                {
                    Content = new StreamContent(new OnCompletedReadStream(() => waitTcs.SetResult(0)))
                });
            });

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.RequestBodyDestination, proxyError);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal(0, proxyResponseStream.Length);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.RequestBodyDestination, errorFeature.Error);
        Assert.IsType<IOException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages();
    }

    [Fact]
    public async Task UpgradableRequest_RequestBodyCopyError_CancelsResponseBody()
    {
        var events = TestEventListener.Collect();

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

        var destinationPrefix = "https://localhost/";
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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.UpgradeRequestClient, proxyError);
        Assert.Equal(StatusCodes.Status101SwitchingProtocols, httpContext.Response.StatusCode);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.UpgradeRequestClient, errorFeature.Error);
        Assert.IsType<IOException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(upgrade: true);
    }

    [Fact]
    public async Task UpgradableRequest_ResponseBodyCopyError_CancelsRequestBody()
    {
        var events = TestEventListener.Collect();

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

        var destinationPrefix = "https://localhost/";
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

        var proxyError = await sut.SendAsync(httpContext, destinationPrefix, client);

        Assert.Equal(ForwarderError.UpgradeResponseDestination, proxyError);
        Assert.Equal(StatusCodes.Status101SwitchingProtocols, httpContext.Response.StatusCode);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.UpgradeResponseDestination, errorFeature.Error);
        Assert.IsType<IOException>(errorFeature.Exception);

        AssertProxyStartFailedStop(events, destinationPrefix, httpContext.Response.StatusCode, errorFeature.Error);
        events.AssertContainProxyStages(upgrade: true);
    }

    [Fact]
    public async Task WithHttpClient_Fails()
    {
        var httpClient = new HttpClient();
        var httpContext = new DefaultHttpContext();
        var destinationPrefix = "";
        var transforms = HttpTransformer.Default;
        var requestOptions = ForwarderRequestConfig.Empty;
        var proxy = CreateProxy();

        await Assert.ThrowsAsync<ArgumentException>(async () => await proxy.SendAsync(httpContext,
            destinationPrefix, httpClient, requestOptions, transforms));
    }

    [Theory]
    [InlineData("HTTP/1.1", "1.1")]
    [InlineData("HTTP/2", "2.0")]
    public async Task Expect100ContinueWithFailedResponse_ReturnResponse(string fromProtocol, string toProtocol)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Protocol = fromProtocol;
        httpContext.Request.Headers[HeaderNames.Expect] = "100-continue";
        var content = Encoding.UTF8.GetBytes(new string('a', 1024 * 1024 * 10));
        httpContext.Request.Headers[HeaderNames.ContentLength] = content.Length.ToString();
        using var contentStream = new MemoryStream(content);
        httpContext.Request.Body = contentStream;

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();
                Assert.NotNull(request.Content);
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = Version.Parse(toProtocol) });

        Assert.Equal(0, contentStream.Position);
        Assert.Equal((int)HttpStatusCode.Conflict, httpContext.Response.StatusCode);
    }

    [Theory]
    [InlineData("1.1", false, "Connection: upgrade; Upgrade: test123", null, "Connection; Upgrade")]
    [InlineData("1.1", false, "Connection: keep-alive; Keep-Alive: timeout=100", null, "Connection; Keep-Alive")]
    [InlineData("1.1", true, "Connection: upgrade; Upgrade: websocket", "Connection: upgrade; Upgrade: websocket", null)]
    [InlineData("1.1", true, "Connection: upgrade, keep-alive; Upgrade: websocket; Keep-Alive: timeout=100", "Connection: upgrade; Upgrade: websocket", "Keep-Alive")]
    [InlineData("1.1", true, "Foo: bar; Upgrade: websocket", "Foo: bar", "Upgrade")]
    [InlineData("1.1", true, "Foo: bar; Connection: upgrade", "Foo: bar", "Connection")]
    [InlineData("1.1", false, "Foo: bar", "Foo: bar", null)]
    [InlineData("2.0", false, "Connection: keep-alive; Keep-Alive: timeout=100", null, "Connection; Keep-Alive")]
    [InlineData("2.0", false, "Connection: upgrade; Upgrade: websocket", null, "Connection; Upgrade")]
    [InlineData("2.0", false, "Foo: bar", "Foo: bar", null)]
    public async Task ResponseToNonUpgradeableRequest_RemoveAllConnectionHeaders(string protocol, bool upgrade, string responseHeadersList, string preservedHeadersList, string removedHeadersList)
    {
        var events = TestEventListener.Collect();

        var responseHeaders = responseHeadersList.Split("; ");
        var preservedHeaders = preservedHeadersList?.Split("; ") ?? Enumerable.Empty<string>();
        var removedHeaders = removedHeadersList?.Split("; ") ?? Enumerable.Empty<string>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        if (upgrade)
        {
            var upgradeFeature = new Mock<IHttpUpgradeFeature>();
            upgradeFeature.SetupGet(f => f.IsUpgradableRequest).Returns(true);
            upgradeFeature.Setup(f => f.UpgradeAsync()).ReturnsAsync(httpContext.Request.Body);
            httpContext.Features.Set(upgradeFeature.Object);
            httpContext.Request.Headers[HeaderNames.Upgrade] = "WebSocket";
        }

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                var response = new HttpResponseMessage(upgrade ? HttpStatusCode.SwitchingProtocols : HttpStatusCode.OK);
                response.Content = new StringContent("Foo");

                foreach (var header in responseHeaders)
                {
                    (var headerName, var headerValues) = GetHeaderNameAndValues(header);
                    response.Headers.TryAddWithoutValidation(headerName, headerValues);
                }

                return response;
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = Version.Parse(protocol) });

        Assert.Equal(upgrade ? (int)HttpStatusCode.SwitchingProtocols : (int)HttpStatusCode.OK, httpContext.Response.StatusCode);

        foreach (var preservedHeader in preservedHeaders)
        {
            (var headerName, var expectedValues) = GetHeaderNameAndValues(preservedHeader);
            var actualValues = httpContext.Response.Headers[headerName];
            Assert.Equal(expectedValues, actualValues);
        }

        foreach (var removedHeaderName in removedHeaders)
        {
            Assert.False(httpContext.Response.Headers.TryGetValue(removedHeaderName, out _));
        }

        AssertProxyStartStop(events, destinationPrefix, httpContext.Response.StatusCode);
        events.AssertContainProxyStages(hasRequestContent: upgrade, upgrade);
    }

    [Theory]
    [InlineData("1.1", false, "Connection: upgrade; Upgrade: test123", null, "Connection; Upgrade")]
    [InlineData("1.1", false, "Connection: keep-alive; Keep-Alive: timeout=100", null, "Connection; Keep-Alive")]
    [InlineData("1.1", true, "Connection: upgrade; Upgrade: websocket", "Connection: upgrade; Upgrade: websocket", null)]
    [InlineData("1.1", true, "Connection: upgrade; Upgrade: SPDY/", "Connection: upgrade; Upgrade: SPDY/", null)]
    [InlineData("1.1", true, "Connection: upgrade, keep-alive; Upgrade: websocket; Keep-Alive: timeout=100", "Connection: upgrade; Upgrade: websocket", "Keep-Alive")]
    [InlineData("1.1", false, "Foo: bar", "Foo: bar", null)]
    [InlineData("2.0", false, "Connection: keep-alive; Keep-Alive: timeout=100", null, "Connection; Keep-Alive")]
    [InlineData("2.0", false, "Connection: upgrade; Upgrade: websocket", null, "Connection; Upgrade")]
    [InlineData("2.0", false, "Foo: bar", "Foo: bar", null)]
    public async Task NonUpgradableRequest_RemoveAllConnectionHeaders(string protocol, bool upgrade, string addHeadersList, string preservedHeadersList, string removedHeadersList)
    {
        var addHeaders = addHeadersList.Split("; ").Select(GetHeaderNameAndValues);
        var preservedHeaders = (preservedHeadersList?.Split("; ") ?? Enumerable.Empty<string>()).Select(GetHeaderNameAndValues);
        var removedHeaders = removedHeadersList?.Split("; ") ?? Enumerable.Empty<string>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        if (upgrade)
        {
            var upgradeFeature = new Mock<IHttpUpgradeFeature>();
            upgradeFeature.SetupGet(f => f.IsUpgradableRequest).Returns(true);
            httpContext.Features.Set(upgradeFeature.Object);
        }

        foreach (var (name, value) in addHeaders)
        {
            httpContext.Request.Headers[name] = value;
        }

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                foreach (var (name, value) in preservedHeaders)
                {
                    var actualValues = string.Join(", ", request.Headers.GetValues(name));
                    Assert.Equal(value, actualValues);
                }

                foreach (var removedHeaderName in removedHeaders)
                {
                    Assert.False(request.Headers.TryGetValues(removedHeaderName, out _));
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                return response;
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = Version.Parse(protocol) });

        Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(GetProhibitedHeaders))]
    [MemberData(nameof(GetHeadersWithNewLines))]
    public async Task Request_RemoveProhibitedHeaders(string protocol, string prohibitedHeadersList)
    {
        const string PreservedHeaderName = "Foo";
        const string PreservedHeaderValue = "bar";
        var prohibitedHeaders = prohibitedHeadersList.Split("; ").Select(GetHeaderNameAndValues);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        foreach (var (name, value) in prohibitedHeaders)
        {
            httpContext.Request.Headers[name] = value;
        }
        httpContext.Request.Headers[PreservedHeaderName] = PreservedHeaderValue;

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                Assert.Equal(PreservedHeaderValue, string.Join(", ", request.Headers.GetValues(PreservedHeaderName)));

                foreach (var (name, _) in prohibitedHeaders)
                {
                    Assert.False(request.Headers.TryGetValues(name, out _));
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                return response;
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = Version.Parse(protocol) });

        Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);
    }

#if NET6_0_OR_GREATER
    [Theory]
    [MemberData(nameof(ResponseMultiHeadersData))]
    public async Task ResponseWithMultiHeaders(string version, string headerName, string[] headers)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (!response.Headers.TryAddWithoutValidation(headerName, headers))
                {
                    Assert.True(response.Content.Headers.TryAddWithoutValidation(headerName, headers));
                }

                return response;
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = new Version(version) });

        Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);
        Assert.True(httpContext.Response.Headers.TryGetValue(headerName, out var sentHeaders));
        Assert.True(sentHeaders.Equals(headers));
    }
#endif

    [Theory]
    [MemberData(nameof(GetProhibitedHeaders))]
    public async Task Response_RemoveProhibitedHeaders(string protocol, string prohibitedHeadersList)
    {
        const string PreservedHeaderName = "Foo";
        const string PreservedHeaderValue = "bar";
        var prohibitedHeaders = prohibitedHeadersList.Split("; ").Select(GetHeaderNameAndValues);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        var destinationPrefix = "https://localhost:123/a/b/";
        var sut = CreateProxy();
        var client = MockHttpHandler.CreateClient(
            async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Yield();

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                foreach (var (name, value) in prohibitedHeaders)
                {
                    response.Headers.TryAddWithoutValidation(name, value);
                }
                response.Headers.TryAddWithoutValidation(PreservedHeaderName, PreservedHeaderValue);

                return response;
            });

        await sut.SendAsync(httpContext, destinationPrefix, client, new ForwarderRequestConfig { Version = Version.Parse(protocol) });

        Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);
        Assert.Equal(PreservedHeaderValue, string.Join(", ", httpContext.Response.Headers[PreservedHeaderName]));

        foreach (var (name, _) in prohibitedHeaders)
        {
            Assert.False(httpContext.Response.Headers.TryGetValue(name, out _));
        }
    }

    public static IEnumerable<object[]> GetProhibitedHeaders()
    {
        var headers = new[]
        {
            "Connection: close",
            "Upgrade: test123",
            "Transfer-Encoding: deflate",
            "Keep-Alive: timeout=100",
            "Proxy-Connection: value",
            "Proxy-Authenticate: value",
            "Proxy-Authentication-Info: value",
            "Proxy-Authorization: value",
            "Proxy-Features: value",
            "Proxy-Instruction: value",
            "Security-Scheme: value",
            "ALPN: value",
            "Close: value",
            "TE: value",
            "HTTP2-Settings: value",
            "Upgrade-Insecure-Requests: value",
            "Alt-Svc: value",
#if NET6_0_OR_GREATER
            "traceparent: value",
            "Request-Id: value",
            "tracestate: value",
            "baggage: value",
            "Correlation-Context: value",
#endif
        };

        foreach (var header in headers)
        {
            yield return new object[] { "1.1", header };
            yield return new object[] { "2.0", header };
        }
    }

    public static IEnumerable<object[]> GetHeadersWithNewLines()
    {
        var headers = new[]
        {
            "valid-name-1: \rfoo",
            "valid-name-2: bar\n",
            "valid-name-3: foo\r\nbar",
            "valid-name-4: foo\r\n bar",
        };

        foreach (var header in headers)
        {
            yield return new object[] { "1.1", header };
            yield return new object[] { "2.0", header };
        }
    }

    private static void AssertProxyStartStop(List<EventWrittenEventArgs> events, string destinationPrefix, int statusCode)
    {
        AssertProxyStartFailedStop(events, destinationPrefix, statusCode, error: null);
    }

    private static void AssertProxyStartFailedStop(List<EventWrittenEventArgs> events, string destinationPrefix, int statusCode, ForwarderError? error)
    {
        var start = Assert.Single(events, e => e.EventName == "ForwarderStart");
        var prefixActual = (string)Assert.Single(start.Payload);
        Assert.Equal(destinationPrefix, prefixActual);

        var stop = Assert.Single(events, e => e.EventName == "ForwarderStop");
        var statusActual = (int)Assert.Single(stop.Payload);
        Assert.Equal(statusCode, statusActual);
        Assert.True(start.TimeStamp <= stop.TimeStamp);

        if (error is null)
        {
            Assert.DoesNotContain(events, e => e.EventName == "ForwarderFailed");
        }
        else
        {
            var failed = Assert.Single(events, e => e.EventName == "ForwarderFailed");
            var errorActual = (ForwarderError)Assert.Single(failed.Payload);
            Assert.Equal(error.Value, errorActual);
            Assert.True(start.TimeStamp <= failed.TimeStamp);
            Assert.True(failed.TimeStamp <= stop.TimeStamp);
        }
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

    private static (string Name, string Values) GetHeaderNameAndValues(string fullHeader)
    {
        var headerNameEnd = fullHeader.IndexOf(": ");
        return (fullHeader.Substring(0, headerNameEnd), fullHeader.Substring(headerNameEnd + 2));
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

    private class ThrowStream : DelegatingStream
    {
        private bool _firstRead = true;

        public ThrowStream(bool throwOnFirstRead = true)
            : base(Stream.Null)
        {
            ThrowOnFirstRead = throwOnFirstRead;
        }

        public bool ThrowOnFirstRead { get; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
            {
                return new ValueTask<int>(0);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_firstRead && !ThrowOnFirstRead)
            {
                _firstRead = false;
                return new ValueTask<int>(1);
            }
            throw new IOException("Fake connection issue");
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("Fake connection issue");
        }
    }

    private class StallStream : DelegatingStream
    {
        public StallStream(Task until)
            : this(_ => until)
        { }

        public StallStream(Func<CancellationToken, Task> onStallAction)
            : base(Stream.Null)
        {
            OnStallAction = onStallAction;
        }

        public Func<CancellationToken, Task> OnStallAction { get; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await OnStallAction(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await OnStallAction(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException();
        }
    }

    private class CallbackReadStream : DelegatingStream
    {
        public CallbackReadStream(Func<Memory<byte>, CancellationToken, ValueTask<int>> onReadAsync)
            : base(Stream.Null)
        {
            OnReadAsync = onReadAsync;
        }

        public Func<Memory<byte>, CancellationToken, ValueTask<int>> OnReadAsync { get; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return OnReadAsync(buffer, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException();
        }
    }

    private class TestResponseBody : DelegatingStream, IHttpResponseBodyFeature, IHttpResponseFeature, IHttpRequestLifetimeFeature
    {
        public TestResponseBody()
            : this(new MemoryStream())
        { }

        public TestResponseBody(Stream innerStream)
            : base(innerStream)
        {
            InnerStream = innerStream;
        }

        public Stream InnerStream { get; }

        public bool Aborted { get; private set; }

        public Stream Stream => this;

        public PipeWriter Writer => throw new NotImplementedException();

        public bool BufferingDisabled { get; set; }

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
            BufferingDisabled = true;
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
            throw new NotImplementedException();
        }

        public void OnStarting(Func<object, Task> callback, object state)
        {
            throw new NotImplementedException();
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            OnStart();
            return base.WriteAsync(buffer, cancellationToken);
        }

        private void OnStart()
        {
            if (!HasStarted)
            {
                HasStarted = true;
            }
        }
    }

    private class OnCompletedReadStream : DelegatingStream
    {
        public OnCompletedReadStream(Action onCompleted)
            : base(Stream.Null)
        {
            OnCompleted = onCompleted;
        }

        public Action OnCompleted { get; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length != 0)
            {
                OnCompleted();
            }
            return new ValueTask<int>(0);
        }
    }

    private class DelegateHttpTransforms : HttpTransformer
    {
        public bool CopyRequestHeaders { get; set; } = true;

        public Func<HttpContext, HttpRequestMessage, string, Task> OnRequest { get; set; } = (_, _, _) => Task.CompletedTask;
        public Func<HttpContext, HttpResponseMessage, ValueTask<bool>> OnResponse { get; set; } = (_, _) => new(true);
        public Func<HttpContext, HttpResponseMessage, Task> OnResponseTrailers { get; set; } = (_, _) => Task.CompletedTask;

        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            if (CopyRequestHeaders)
            {
                await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
            }

            await OnRequest(httpContext, proxyRequest, destinationPrefix);
        }

        public override async ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            await base.TransformResponseAsync(httpContext, proxyResponse);

            return await OnResponse(httpContext, proxyResponse);
        }

        public override async ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            await base.TransformResponseTrailersAsync(httpContext, proxyResponse);

            await OnResponseTrailers(httpContext, proxyResponse);
        }
    }
}
