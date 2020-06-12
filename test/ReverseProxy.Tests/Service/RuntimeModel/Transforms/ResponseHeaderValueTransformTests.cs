// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
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
        public void AddResponseHeader_Success(string startValue, int status, string value, bool append, bool always, string expected)
        {
            var httpContext = new DefaultHttpContext();
            var response = new HttpResponseMessage();
            httpContext.Response.StatusCode = status;
            var transform = new ResponseHeaderValueTransform(value, append, always);
            var result = transform.Apply(httpContext, response, startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), result);
        }
    }
}
