// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderValueTransformTests
    {
        [Theory]
        // Using ";" to represent multi-line headers
        [InlineData("", "new,value", false, "new,value")]
        [InlineData("", "new,value", true, "new,value")]
        [InlineData("start", "new,value", false, "new,value")]
        [InlineData("start,value", "new,value", false, "new,value")]
        [InlineData("start;value", "new,value", false, "new,value")]
        [InlineData("start", "new,value", true, "start;new,value")]
        [InlineData("start,value", "new,value", true, "start,value;new,value")]
        [InlineData("start;value", "new,value", true, "start;value;new,value")]
        public void AddHeader_Success(string startValue, string value, bool append, string expected)
        {
            var httpContext = new DefaultHttpContext();
            var transform = new RequestHeaderValueTransform(value, append);
            var result = transform.Apply(httpContext, new HttpRequestMessage(), startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), result);
        }
    }
}
