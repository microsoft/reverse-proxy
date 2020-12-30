// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderXForwardedHostTransformTests
    {
        [Theory]
        // Using ";" to represent multi-line headers
        [InlineData("", "", false, "")]
        [InlineData("", "", true, "")]
        [InlineData("", "host", false, "host")]
        [InlineData("", "host:80", true, "host:80")]
        [InlineData("", "ho本st", false, "xn--host-6j1i")]
        [InlineData("", "::1", false, "::1")]
        [InlineData("", "[::1]:80", false, "[::1]:80")]
        [InlineData("existing,Header", "", false, "")]
        [InlineData("existing;Header", "", false, "")]
        [InlineData("existing,Header", "", true, "existing,Header")]
        [InlineData("existing;Header", "", true, "existing;Header")]
        [InlineData("existing,Header", "host", false, "host")]
        [InlineData("existing;Header", "host", false, "host")]
        [InlineData("existing,Header", "host:80", true, "existing,Header;host:80")]
        [InlineData("existing;Header", "host", true, "existing;Header;host")]
        public async Task Host_Added(string startValue, string host, bool append, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = string.IsNullOrEmpty(host) ? new HostString() : new HostString(host);
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderXForwardedHostTransform("name", append);
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
