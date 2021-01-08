// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class ResponseTrailerValueTransformTests
    {
        [Theory]
        // Using ";" to represent multi-line headers
        [InlineData("", 400, "new", false, false, "")]
        [InlineData("", 200, "new", false, false, "new")]
        [InlineData("", 400, "new", false, true, "new")]
        [InlineData("", 200, "new", false, true, "new")]
        [InlineData("start", 400, "new", false, false, "start")]
        [InlineData("start", 200, "new", false, false, "new")]
        [InlineData("start", 400, "new", false, true, "new")]
        [InlineData("start", 200, "new", false, true, "new")]
        [InlineData("start", 400, "new", true, false, "start")]
        [InlineData("start", 200, "new", true, false, "start;new")]
        [InlineData("start", 400, "new", true, true, "start;new")]
        [InlineData("start", 200, "new", true, true, "start;new")]
        [InlineData("start,value", 400, "new", true, false, "start,value")]
        [InlineData("start,value", 200, "new", true, false, "start,value;new")]
        [InlineData("start,value", 400, "new", true, true, "start,value;new")]
        [InlineData("start,value", 200, "new", true, true, "start,value;new")]
        [InlineData("start;value", 400, "new", true, false, "start;value")]
        [InlineData("start;value", 200, "new", true, false, "start;value;new")]
        [InlineData("start;value", 400, "new", true, true, "start;value;new")]
        [InlineData("start;value", 200, "new", true, true, "start;value;new")]
        public async Task AddResponseTrailer_Success(string startValue, int status, string value, bool append, bool always, string expected)
        {
            var httpContext = new DefaultHttpContext();
            var trailerFeature = new TestTrailersFeature();
            httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
            trailerFeature.Trailers["name"] = startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries);
            httpContext.Response.StatusCode = status;
            var transformContext = new ResponseTrailersTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = new HttpResponseMessage(),
                HeadersCopied = true,
            };
            var transform = new ResponseTrailerValueTransform("name", value, append, always);
            await transform.ApplyAsync(transformContext);
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), trailerFeature.Trailers["name"]);
        }

        private class TestTrailersFeature : IHttpResponseTrailersFeature
        {
            public IHeaderDictionary Trailers { get; set; } = new HeaderDictionary();
        }
    }
}
