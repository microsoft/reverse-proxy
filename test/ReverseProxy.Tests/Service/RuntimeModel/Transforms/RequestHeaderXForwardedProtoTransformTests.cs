// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
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
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("name", startValue.Split(";", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderXForwardedProtoTransform("name", append);
            transform.Apply(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            });
            Assert.Equal(expected.Split(";", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("name"));
        }
    }
}
