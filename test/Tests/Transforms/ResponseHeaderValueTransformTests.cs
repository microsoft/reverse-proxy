// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests
{
    public class ResponseHeaderValueTransformTests
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
        public async Task AddResponseHeader_Success(string startValue, int status, string value, bool append, bool always, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Headers["name"] = startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries);
            httpContext.Response.StatusCode = status;
            var transformContext = new ResponseTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = new HttpResponseMessage(),
                HeadersCopied = true,
            };
            var transform = new ResponseHeaderValueTransform("name", value, append, always);
            await transform.ApplyAsync(transformContext);
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), httpContext.Response.Headers["name"]);
        }
    }
}
