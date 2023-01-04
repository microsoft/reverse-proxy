// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class HttpTransformerTests
{
    private static readonly string[] RestrictedHeaders = new[]
    {
        HeaderNames.Connection,
        HeaderNames.TransferEncoding,
        HeaderNames.KeepAlive,
        HeaderNames.Upgrade,
        HeaderNames.ProxyConnection,
        HeaderNames.ProxyAuthenticate,
        "Proxy-Authentication-Info",
        HeaderNames.ProxyAuthorization,
        "Proxy-Features",
        "Proxy-Instruction",
        "Security-Scheme",
        "ALPN",
        "Close",
        "HTTP2-Settings",
        HeaderNames.UpgradeInsecureRequests,
        HeaderNames.TE,
        HeaderNames.AltSvc,
        HeaderNames.StrictTransportSecurity,
    };

    [Fact]
    public async Task TransformRequestAsync_RemovesRestrictedHeaders()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost");

        foreach (var header in RestrictedHeaders)
        {
            httpContext.Request.Headers[header] = "value";
        }

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix");

        foreach (var header in RestrictedHeaders)
        {
            Assert.False(proxyRequest.Headers.Contains(header));
        }

        Assert.Null(proxyRequest.Content);
    }

    [Fact]
    public async Task TransformRequestAsync_KeepOriginalHost()
    {
        var transformer = HttpTransformer.Empty;
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost");

        httpContext.Request.Host = new HostString("example.com:3456");

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix");

        Assert.Equal("example.com:3456", proxyRequest.Headers.Host);
    }

    [Fact]
    public async Task TransformRequestAsync_TETrailers_Copied()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Protocol = "HTTP/2";
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost");

        httpContext.Request.Headers[HeaderNames.TE] = "traiLers";

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix");

        Assert.True(proxyRequest.Headers.TryGetValues(HeaderNames.TE, out var values));
        var value = Assert.Single(values);
        Assert.Equal("traiLers", value);

        Assert.Null(proxyRequest.Content);
    }

    [Fact]
    public async Task TransformRequestAsync_ContentLengthAndTransferEncoding_ContentLengthRemoved()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://localhost")
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        httpContext.Request.Headers[HeaderNames.TransferEncoding] = "chUnked";
        httpContext.Request.Headers[HeaderNames.ContentLength] = "10";

        await transformer.TransformRequestAsync(httpContext, proxyRequest, "prefix");

        Assert.False(proxyRequest.Content.Headers.TryGetValues(HeaderNames.ContentLength, out var _));
        // Transfer-Encoding is on the restricted list and removed. HttpClient will re-add it if required.
        Assert.False(proxyRequest.Headers.TryGetValues(HeaderNames.TransferEncoding, out var _));
    }

    [Theory]
    [InlineData(HttpStatusCode.Continue)]
    [InlineData(HttpStatusCode.SwitchingProtocols)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.ResetContent)]
    public async Task TransformResponseAsync_ContentLength0OnBodylessStatusCode_ContentLengthRemoved(HttpStatusCode statusCode)
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();

        var proxyResponse = new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(new byte[0])
        };

        Assert.Equal(0, proxyResponse.Content.Headers.ContentLength);
        await transformer.TransformResponseAsync(httpContext, proxyResponse);
        Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.ContentLength));
    }

    [Fact]
    public async Task TransformResponseAsync_RemovesRestrictedHeaders()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage()
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        foreach (var header in RestrictedHeaders)
        {
            if (!proxyResponse.Headers.TryAddWithoutValidation(header, "value"))
            {
                Assert.True(proxyResponse.Content.Headers.TryAddWithoutValidation(header, "value"));
            }
        }

        await transformer.TransformResponseAsync(httpContext, proxyResponse);

        foreach (var header in RestrictedHeaders)
        {
            Assert.False(httpContext.Response.Headers.ContainsKey(header));
        }
    }

    [Fact]
    public async Task TransformResponseAsync_ContentLengthAndTransferEncoding_ContentLengthRemoved()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var proxyResponse = new HttpResponseMessage()
        {
            Content = new ByteArrayContent(new byte[10])
        };

        proxyResponse.Headers.TransferEncodingChunked = true;
        Assert.Equal(10, proxyResponse.Content.Headers.ContentLength);

        await transformer.TransformResponseAsync(httpContext, proxyResponse);

        Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.ContentLength));
        // Transfer-Encoding is on the restricted list and removed. HttpClient will re-add it if required.
        Assert.False(httpContext.Response.Headers.ContainsKey(HeaderNames.TransferEncoding));
    }

    [Fact]
    public async Task TransformResponseTrailersAsync_RemovesRestrictedHeaders()
    {
        var transformer = HttpTransformer.Default;
        var httpContext = new DefaultHttpContext();
        var trailersFeature = new TestTrailersFeature();
        httpContext.Features.Set<IHttpResponseTrailersFeature>(trailersFeature);
        var proxyResponse = new HttpResponseMessage();

        foreach (var header in RestrictedHeaders)
        {
            Assert.True(proxyResponse.TrailingHeaders.TryAddWithoutValidation(header, "value"));
        }

        await transformer.TransformResponseTrailersAsync(httpContext, proxyResponse);

        foreach (var header in RestrictedHeaders)
        {
            Assert.False(trailersFeature.Trailers.ContainsKey(header));
        }
    }

    private class TestTrailersFeature : IHttpResponseTrailersFeature
    {
        public IHeaderDictionary Trailers { get; set; } = new HeaderDictionary();
    }
}
