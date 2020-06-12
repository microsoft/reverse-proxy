// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderXForwardedProtoTransformTests
    {
        [Theory]
        // Using ";" to represent multi-line headers
        [InlineData("", "http", false, "http")]
        [InlineData("", "http", true, "http")]
        [InlineData("existing,Header", "http", false, "http")]
        [InlineData("existing;Header", "http", false, "http")]
        [InlineData("existing,Header", "http", true, "existing,Header;http")]
        [InlineData("existing;Header", "http", true, "existing;Header;http")]
        public void Scheme_Added(string startValue, string scheme, bool append, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = scheme;
            var transform = new RequestHeaderXForwardedProtoTransform(append);
            var result = transform.Apply(httpContext, startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), result);
        }
    }
}
