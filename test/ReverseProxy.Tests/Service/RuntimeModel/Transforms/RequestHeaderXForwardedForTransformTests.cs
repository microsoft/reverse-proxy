// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
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
        public void RemoteIp_Added(string startValue, string remoteIp, bool append, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = string.IsNullOrEmpty(remoteIp) ? null : IPAddress.Parse(remoteIp);
            var transform = new RequestHeaderXForwardedForTransform(append);
            var result = transform.Apply(httpContext, new HttpRequestMessage(), startValue.Split(";", System.StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(expected.Split(";", System.StringSplitOptions.RemoveEmptyEntries), result);
        }
    }
}
