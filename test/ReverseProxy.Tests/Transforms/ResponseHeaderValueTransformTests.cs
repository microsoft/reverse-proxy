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
        [InlineData("", 400, "new", false, false, "", false)]
        [InlineData("", 502, "new", false, false, "", true)]
        [InlineData("", 200, "new", false, false, "new", false)]
        [InlineData("", 400, "new", false, true, "new", false)]
        [InlineData("", 200, "new", false, true, "new", false)]
        [InlineData("", 502, "new", false, true, "new", true)]
        [InlineData("start", 400, "new", false, false, "start", false)]
        [InlineData("start", 200, "new", false, false, "new", false)]
        [InlineData("start", 502, "new", false, false, "start", true)]
        [InlineData("start", 400, "new", false, true, "new", false)]
        [InlineData("start", 200, "new", false, true, "new", false)]
        [InlineData("start", 400, "new", true, false, "start", false)]
        [InlineData("start", 200, "new", true, false, "start;new", false)]
        [InlineData("start", 400, "new", true, true, "start;new", false)]
        [InlineData("start", 200, "new", true, true, "start;new", false)]
        [InlineData("start,value", 400, "new", true, false, "start,value", false)]
        [InlineData("start,value", 200, "new", true, false, "start,value;new", false)]
        [InlineData("start,value", 400, "new", true, true, "start,value;new", false)]
        [InlineData("start,value", 200, "new", true, true, "start,value;new", false)]
        [InlineData("start;value", 400, "new", true, false, "start;value", false)]
        [InlineData("start;value", 200, "new", true, false, "start;value;new", false)]
        [InlineData("start;value", 400, "new", true, true, "start;value;new", false)]
        [InlineData("start;value", 200, "new", true, true, "start;value;new", false)]
        public async Task AddResponseHeader_Success(string startValue, int status, string value, bool append, bool always, string expected, bool responseNull)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Headers["name"] = startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries);
            httpContext.Response.StatusCode = status;
            var transformContext = new ResponseTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = responseNull ? null :  new HttpResponseMessage(),
                HeadersCopied = true,
            };
            var transform = new ResponseHeaderValueTransform("name", value, append, always);
            await transform.ApplyAsync(transformContext);
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), httpContext.Response.Headers["name"]);
        }
    }
}
