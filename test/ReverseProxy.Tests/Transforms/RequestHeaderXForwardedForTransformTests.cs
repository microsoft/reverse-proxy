// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Service.Model.Transforms
{
    public class RequestHeaderXForwardedForTransformTests
    {
        [Theory]
        // Using ";" to represent multi-line headers
        [InlineData("", "", false, "")]
        [InlineData("", "", true, "")]
        [InlineData("", "::1", false, "::1")]
        [InlineData("", "127.0.0.1", false, "127.0.0.1")]
        [InlineData("", "127.0.0.1", true, "127.0.0.1")]
        [InlineData("existing,Header", "", false, "")]
        [InlineData("existing;Header", "", false, "")]
        [InlineData("existing,Header", "", true, "existing,Header")]
        [InlineData("existing;Header", "", true, "existing;Header")]
        [InlineData("existing,Header", "127.0.0.1", false, "127.0.0.1")]
        [InlineData("existing;Header", "127.0.0.1", false, "127.0.0.1")]
        [InlineData("existing,Header", "127.0.0.1", true, "existing,Header;127.0.0.1")]
        [InlineData("existing;Header", "127.0.0.1", true, "existing;Header;127.0.0.1")]
        public async Task RemoteIp_Added(string startValue, string remoteIp, bool append, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = string.IsNullOrEmpty(remoteIp) ? null : IPAddress.Parse(remoteIp);
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderXForwardedForTransform("name", append);
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
