// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Xunit;

namespace Yarp.ReverseProxy.Service.Proxy.Tests
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

        [Fact]
        public void GetHttpMethod_Unknown_Works()
        {
            Assert.Same("Unknown", HttpUtilities.GetHttpMethod("Unknown").Method);
        }

        [Fact]
        public void GetHttpMethod_Connect_Throws()
        {
            Assert.Throws<NotSupportedException>(() => HttpUtilities.GetHttpMethod("CONNECT"));
        }

        [Theory]
        [InlineData(" GET")]
        [InlineData("GET ")]
        [InlineData("G;ET")]
        public void GetHttpMethod_Invalid_Throws(string method)
        {
            Assert.Throws<FormatException>(() => HttpUtilities.GetHttpMethod(method));
        }
    }
}
