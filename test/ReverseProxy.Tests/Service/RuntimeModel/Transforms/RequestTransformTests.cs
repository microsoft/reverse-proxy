// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestTransformTests
    {
        [Fact]
        public void TakeHeader_HeadersNotCopied_ReturnsHttpRequestHeader()
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
                HeadersCopied = false,
            }, "name");
            Assert.Equal("value0", result);
            Assert.Equal(new[] { "value1" }, proxyRequest.Headers.GetValues("name"));
            Assert.Equal(new[] { "value2" }, proxyRequest.Content.Headers.GetValues("name"));
        }

        [Fact]
        public void TakeHeader_HeadersCopied_RemovesAndReturnsProxyRequestHeader()
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
                HeadersCopied = true,
            }, "name");
            Assert.Equal("value1", result);
            Assert.False(proxyRequest.Headers.TryGetValues("name", out var _));
            Assert.Equal(new[] { "value2" }, proxyRequest.Content.Headers.GetValues("name"));
        }

        [Fact]
        public void TakeHeaderFromContent_HeadersCopied_RemovesAndReturnsProxyContentHeader()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentType = "value0";
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Content = new StringContent("hello world");
            var result = RequestTransform.TakeHeader(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            }, HeaderNames.ContentType);
            Assert.Equal("text/plain; charset=utf-8", result);
            Assert.False(proxyRequest.Content.Headers.TryGetValues(HeaderNames.ContentType, out var _));
        }
    }
}
