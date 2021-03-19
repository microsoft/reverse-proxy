// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
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
        public async Task PathBase_Added(string startValue, string pathBase, bool append, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.PathBase = string.IsNullOrEmpty(pathBase) ? new PathString() : new PathString(pathBase);
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderXForwardedPathBaseTransform("name", append);
            await transform.ApplyAsync(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            });
            if (string.IsNullOrEmpty(expected))
            {
                Assert.False(proxyRequest.Headers.TryGetValues("name", out var _));
            }
            else
            {
                Assert.Equal(expected.Split(";", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("name"));
            }
        }
    }
}
