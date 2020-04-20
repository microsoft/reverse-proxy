// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
{
    public class HttpUtilitiesTests
    {
        [Fact]
        public void GetHttpMethod_Get_Works()
        {
            Assert.Same(HttpMethod.Get, HttpUtilities.GetHttpMethod("GET"));
        }

        [Fact]
        public void GetHttpMethod_Post_Works()
        {
            Assert.Same(HttpMethod.Post, HttpUtilities.GetHttpMethod("POST"));
        }

        [Fact]
        public void GetHttpMethod_Put_Works()
        {
            Assert.Same(HttpMethod.Put, HttpUtilities.GetHttpMethod("PUT"));
        }

        [Fact]
        public void GetHttpMethod_Delete_Works()
        {
            Assert.Same(HttpMethod.Delete, HttpUtilities.GetHttpMethod("DELETE"));
        }

        [Fact]
        public void GetHttpMethod_Options_Works()
        {
            Assert.Same(HttpMethod.Options, HttpUtilities.GetHttpMethod("OPTIONS"));
        }

        [Fact]
        public void GetHttpMethod_Head_Works()
        {
            Assert.Same(HttpMethod.Head, HttpUtilities.GetHttpMethod("HEAD"));
        }

        [Fact]
        public void GetHttpMethod_Patch_Works()
        {
            Assert.Same(HttpMethod.Patch, HttpUtilities.GetHttpMethod("PATCH"));
        }

        [Fact]
        public void GetHttpMethod_Trace_Works()
        {
            Assert.Same(HttpMethod.Trace, HttpUtilities.GetHttpMethod("TRACE"));
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
            Assert.Throws<InvalidOperationException>(action);
        }

        [Theory]
        [InlineData("HTTP/2")]
        [InlineData("http/2")]
        [InlineData("hTtP/2")]
        public void IsHttp2_TrueCases_ReturnsTrue(string protocol)
        {
            Assert.True(HttpUtilities.IsHttp2(protocol));
        }

        [Theory]
        [InlineData("HTTP/1")]
        [InlineData("HTTP/1.1")]
        [InlineData("http/2 ")]
        [InlineData(" http/2")]
        public void IsHttp2_FalseCases_ReturnsFalse(string protocol)
        {
            Assert.False(HttpUtilities.IsHttp2(protocol));
        }
    }
}
