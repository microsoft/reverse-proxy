// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests
{
    public class RequestTransformTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TakeHeader_RemovesAndReturnsProxyRequestHeader(bool copiedHeaders)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add("name", "value0");
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("Name", "value1");
            proxyRequest.Content = new StringContent("hello world");
            proxyRequest.Content.Headers.Add("Name", "value2");
            var result = RequestTransform.TakeHeader(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = copiedHeaders,
            }, "name");
            Assert.Equal("value1", result);
            Assert.False(proxyRequest.Headers.TryGetValues("name", out var _));
            Assert.Equal(new[] { "value2" }, proxyRequest.Content.Headers.GetValues("name"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TakeHeaderFromContent_RemovesAndReturnsProxyContentHeader(bool copiedHeaders)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentType = "value0";
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Content = new StringContent("hello world");
            var result = RequestTransform.TakeHeader(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = copiedHeaders,
            }, HeaderNames.ContentType);
            Assert.Equal("text/plain; charset=utf-8", result);
            Assert.False(proxyRequest.Content.Headers.TryGetValues(HeaderNames.ContentType, out var _));
        }

        [Fact]
        public void TakeHeader_HeadersNotCopied_ReturnsHttpRequestHeader()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add("name", "value0");
            var proxyRequest = new HttpRequestMessage();
            var result = RequestTransform.TakeHeader(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = false,
            }, "name");
            Assert.Equal("value0", result);
        }

        [Fact]
        public void TakeHeader_HeadersCopied_ReturnsNothing()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add("name", "value0");
            var proxyRequest = new HttpRequestMessage();
            var result = RequestTransform.TakeHeader(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            }, "name");
            Assert.Equal(StringValues.Empty, result);
        }
    }
}
