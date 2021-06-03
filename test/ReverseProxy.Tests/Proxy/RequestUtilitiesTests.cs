// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Proxy.Tests
{
    public class RequestUtilitiesTests
    {
        [Fact]
        public void GetHttpMethod_Get_Works()
        {
            Assert.Same(HttpMethod.Get, RequestUtilities.GetHttpMethod("GET"));
        }

        [Fact]
        public void GetHttpMethod_Post_Works()
        {
            Assert.Same(HttpMethod.Post, RequestUtilities.GetHttpMethod("POST"));
        }

        [Fact]
        public void GetHttpMethod_Put_Works()
        {
            Assert.Same(HttpMethod.Put, RequestUtilities.GetHttpMethod("PUT"));
        }

        [Fact]
        public void GetHttpMethod_Delete_Works()
        {
            Assert.Same(HttpMethod.Delete, RequestUtilities.GetHttpMethod("DELETE"));
        }

        [Fact]
        public void GetHttpMethod_Options_Works()
        {
            Assert.Same(HttpMethod.Options, RequestUtilities.GetHttpMethod("OPTIONS"));
        }

        [Fact]
        public void GetHttpMethod_Head_Works()
        {
            Assert.Same(HttpMethod.Head, RequestUtilities.GetHttpMethod("HEAD"));
        }

        [Fact]
        public void GetHttpMethod_Patch_Works()
        {
            Assert.Same(HttpMethod.Patch, RequestUtilities.GetHttpMethod("PATCH"));
        }

        [Fact]
        public void GetHttpMethod_Trace_Works()
        {
            Assert.Same(HttpMethod.Trace, RequestUtilities.GetHttpMethod("TRACE"));
        }

        [Fact]
        public void GetHttpMethod_Unknown_Works()
        {
            Assert.Same("Unknown", RequestUtilities.GetHttpMethod("Unknown").Method);
        }

        [Fact]
        public void GetHttpMethod_Connect_Throws()
        {
            Assert.Throws<NotSupportedException>(() => RequestUtilities.GetHttpMethod("CONNECT"));
        }

        [Theory]
        [InlineData(" GET")]
        [InlineData("GET ")]
        [InlineData("G;ET")]
        public void GetHttpMethod_Invalid_Throws(string method)
        {
            Assert.Throws<FormatException>(() => RequestUtilities.GetHttpMethod(method));
        }

        [Theory]
        [InlineData("http://localhost", "", "", "http://localhost/")]
        [InlineData("http://localhost/", "", "", "http://localhost/")]
        [InlineData("http://localhost", "/", "", "http://localhost/")]
        [InlineData("http://localhost/", "/", "", "http://localhost/")]
        [InlineData("http://localhost", "", "?query", "http://localhost/?query")]
        [InlineData("http://localhost", "/path", "?query", "http://localhost/path?query")]
        [InlineData("http://localhost", "/path/", "?query", "http://localhost/path/?query")]
        [InlineData("http://localhost/", "/path", "?query", "http://localhost/path?query")]
        [InlineData("http://localhost/base", "", "", "http://localhost/base")]
        [InlineData("http://localhost/base", "", "?query", "http://localhost/base?query")]
        [InlineData("http://localhost/base", "/path", "?query", "http://localhost/base/path?query")]
        [InlineData("http://localhost/base/", "/path", "?query", "http://localhost/base/path?query")]
        [InlineData("http://localhost/base/", "/path/", "?query", "http://localhost/base/path/?query")]
        public void MakeDestinationAddress(string destinationPrefix, string path, string query, string expected)
        {
            var uri = RequestUtilities.MakeDestinationAddress(destinationPrefix, new PathString(path), new QueryString(query));
            Assert.Equal(expected, uri.AbsoluteUri);
        }
    }
}
