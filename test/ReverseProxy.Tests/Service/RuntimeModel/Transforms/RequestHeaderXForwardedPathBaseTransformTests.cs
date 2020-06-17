// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderXForwardedPathBaseTransformTests
    {
        [Theory]
        // Using ";" to represent multi-line headers
        [InlineData("", "", false, "")]
        [InlineData("", "", true, "")]
        [InlineData("", "/", false, "/")]
        [InlineData("", "/", true, "/")]
        [InlineData("", "/base", false, "/base")]
        [InlineData("", "/base", true, "/base")]
        [InlineData("", "/base/value", false, "/base/value")]
        [InlineData("", "/base/value", true, "/base/value")]
        [InlineData("", "/base本", false, "/base%E6%9C%AC")]
        [InlineData("existing,Header", "", false, "")]
        [InlineData("existing;Header", "", false, "")]
        [InlineData("existing,Header", "", true, "existing,Header")]
        [InlineData("existing;Header", "", true, "existing;Header")]
        [InlineData("existing,Header", "/base", false, "/base")]
        [InlineData("existing;Header", "/base", false, "/base")]
        [InlineData("existing,Header", "/base", true, "existing,Header;/base")]
        [InlineData("existing;Header", "/base", true, "existing;Header;/base")]
        public void PathBase_Added(string startValue, string pathBase, bool append, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.PathBase = string.IsNullOrEmpty(pathBase) ? new PathString() : new PathString(pathBase);
            var transform = new RequestHeaderXForwardedPathBaseTransform(append);
            var result = transform.Apply(httpContext, startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), result);
        }
    }
}
