// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using FluentAssertions;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
{
    public class HttpUtilitiesTests
    {
        [Fact]
        public void GetHttpMethod_Get_Works()
        {
            HttpUtilities.GetHttpMethod("GET").Should().BeSameAs(HttpMethod.Get);
        }

        [Fact]
        public void GetHttpMethod_Post_Works()
        {
            HttpUtilities.GetHttpMethod("POST").Should().BeSameAs(HttpMethod.Post);
        }

        [Fact]
        public void GetHttpMethod_Put_Works()
        {
            HttpUtilities.GetHttpMethod("PUT").Should().BeSameAs(HttpMethod.Put);
        }

        [Fact]
        public void GetHttpMethod_Delete_Works()
        {
            HttpUtilities.GetHttpMethod("DELETE").Should().BeSameAs(HttpMethod.Delete);
        }

        [Fact]
        public void GetHttpMethod_Options_Works()
        {
            HttpUtilities.GetHttpMethod("OPTIONS").Should().BeSameAs(HttpMethod.Options);
        }

        [Fact]
        public void GetHttpMethod_Head_Works()
        {
            HttpUtilities.GetHttpMethod("HEAD").Should().BeSameAs(HttpMethod.Head);
        }

        [Fact]
        public void GetHttpMethod_Patch_Works()
        {
            HttpUtilities.GetHttpMethod("PATCH").Should().BeSameAs(HttpMethod.Patch);
        }

        [Fact]
        public void GetHttpMethod_Trace_Works()
        {
            HttpUtilities.GetHttpMethod("TRACE").Should().BeSameAs(HttpMethod.Trace);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("get")]
        [InlineData(" GET")]
        [InlineData("GET ")]
        [InlineData("CONNECT")]
        [InlineData("anything")]
        public void GetHttpMethod_Other_Throws(string method)
        {
            // Act
            Action action = () => HttpUtilities.GetHttpMethod(method);

            // Assert
            action.Should().ThrowExactly<InvalidOperationException>();
        }

        [Theory]
        [InlineData("HTTP/2")]
        [InlineData("http/2")]
        [InlineData("hTtP/2")]
        public void IsHttp2_TrueCases_ReturnsTrue(string protocol)
        {
            HttpUtilities.IsHttp2(protocol).Should().BeTrue();
        }

        [Theory]
        [InlineData("HTTP/1")]
        [InlineData("HTTP/1.1")]
        [InlineData("http/2 ")]
        [InlineData(" http/2")]
        public void IsHttp2_FalseCases_ReturnsFalse(string protocol)
        {
            HttpUtilities.IsHttp2(protocol).Should().BeFalse();
        }
    }
}
