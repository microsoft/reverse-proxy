// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class ResponseTrailersTransformTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TakeHeader_RemovesAndReturnsHttpResponseTrailer(bool copiedHeaders)
        {
            var httpContext = new DefaultHttpContext();
            var trailerFeature = new TestTrailersFeature();
            httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
            trailerFeature.Trailers.Add("name", "value0");
            var proxyResponse = new HttpResponseMessage();
            proxyResponse.TrailingHeaders.Add("Name", "value1");
            var result = ResponseTrailersTransform.TakeHeader(new ResponseTrailersTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = proxyResponse,
                HeadersCopied = copiedHeaders,
            }, "name");
            Assert.Equal("value0", result);
            Assert.False(trailerFeature.Trailers.TryGetValue("name", out var _));
            Assert.Equal(new[] { "value1" }, proxyResponse.TrailingHeaders.GetValues("name"));
        }

        [Fact]
        public void TakeHeader_HeadersNotCopied_ReturnsHttpResponseMessageTrailer()
        {
            var httpContext = new DefaultHttpContext();
            var trailerFeature = new TestTrailersFeature();
            httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
            var proxyResponse = new HttpResponseMessage();
            proxyResponse.TrailingHeaders.Add("Name", "value1");
            var result = ResponseTrailersTransform.TakeHeader(new ResponseTrailersTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = proxyResponse,
                HeadersCopied = false,
            }, "name");
            Assert.Equal("value1", result);
            Assert.False(trailerFeature.Trailers.TryGetValue("name", out var _));
            Assert.Equal(new[] { "value1" }, proxyResponse.TrailingHeaders.GetValues("name"));
        }

        [Fact]
        public void TakeHeader_HeadersCopied_ReturnsNothing()
        {
            var httpContext = new DefaultHttpContext();
            var trailerFeature = new TestTrailersFeature();
            httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
            var proxyResponse = new HttpResponseMessage();
            proxyResponse.TrailingHeaders.Add("Name", "value1");
            var result = ResponseTrailersTransform.TakeHeader(new ResponseTrailersTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = proxyResponse,
                HeadersCopied = true,
            }, "name");
            Assert.Equal(StringValues.Empty, result);
            Assert.False(trailerFeature.Trailers.TryGetValue("name", out var _));
            Assert.Equal(new[] { "value1" }, proxyResponse.TrailingHeaders.GetValues("name"));
        }

        private class TestTrailersFeature : IHttpResponseTrailersFeature
        {
            public IHeaderDictionary Trailers { get; set; } = new HeaderDictionary();
        }
    }
}
