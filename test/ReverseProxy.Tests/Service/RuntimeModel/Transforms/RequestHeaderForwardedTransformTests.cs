// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Utilities;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderForwardedTransformTests
    {
        [Theory]
        // Using "|" to represent multi-line headers
        [InlineData("", "https", false, "proto=https")]
        [InlineData("", "https", true, "proto=https")]
        [InlineData("existing,Header", "https", false, "proto=https")]
        [InlineData("existing|Header", "https", false, "proto=https")]
        [InlineData("existing,Header", "https", true, "existing,Header|proto=https")]
        [InlineData("existing|Header", "https", true, "existing|Header|proto=https")]
        public async Task Proto_Added(string startValue, string scheme, bool append, string expected)
        {
            var randomFactory = new TestRandomFactory();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = scheme;
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: NodeFormat.None,
                byFormat: NodeFormat.None, host: false, proto: true, append);
            await transform.ApplyAsync(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            });
            Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
        }

        [Theory]
        // Using "|" to represent multi-line headers
        [InlineData("", "myHost", false, "host=\"myHost\"")]
        [InlineData("", "myHost", true, "host=\"myHost\"")]
        [InlineData("", "ho本st", false, "host=\"xn--host-6j1i\"")]
        [InlineData("", "myHost:80", false, "host=\"myHost:80\"")]
        [InlineData("", "ho本st:80", false, "host=\"xn--host-6j1i:80\"")]
        [InlineData("existing,Header", "myHost", false, "host=\"myHost\"")]
        [InlineData("existing|Header", "myHost", false, "host=\"myHost\"")]
        [InlineData("existing|Header", "myHost:80", false, "host=\"myHost:80\"")]
        [InlineData("existing,Header", "myHost", true, "existing,Header|host=\"myHost\"")]
        [InlineData("existing|Header", "myHost", true, "existing|Header|host=\"myHost\"")]
        [InlineData("existing|Header", "myHost:80", true, "existing|Header|host=\"myHost:80\"")]
        public async Task Host_Added(string startValue, string host, bool append, string expected)
        {
            var randomFactory = new TestRandomFactory();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString(host);
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: NodeFormat.None,
                byFormat: NodeFormat.None, host: true, proto: false, append);
            await transform.ApplyAsync(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            });
            Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
        }

        [Theory]
        // Using "|" to represent multi-line headers
        [InlineData("", "", 2, NodeFormat.Ip, false, "for=unknown")] // Missing IP falls back to Unknown
        [InlineData("", "", 0, NodeFormat.IpAndPort, true, "for=unknown")] // Missing port excluded
        [InlineData("", "", 2, NodeFormat.IpAndPort, true, "for=\"unknown:2\"")]
        [InlineData("", "::1", 2, NodeFormat.Unknown, false, "for=unknown")]
        [InlineData("", "::1", 2, NodeFormat.UnknownAndPort, true, "for=\"unknown:2\"")]
        [InlineData("", "::1", 2, NodeFormat.UnknownAndRandomPort, true, "for=\"unknown:_abcdefghi\"")]
        [InlineData("", "::1", 2, NodeFormat.Ip, false, "for=\"[::1]\"")]
        [InlineData("", "::1", 0, NodeFormat.IpAndPort, true, "for=\"[::1]\"")]
        [InlineData("", "::1", 2, NodeFormat.IpAndPort, true, "for=\"[::1]:2\"")]
        [InlineData("", "::1", 2, NodeFormat.IpAndRandomPort, true, "for=\"[::1]:_abcdefghi\"")]
        [InlineData("", "127.0.0.1", 2, NodeFormat.Ip, false, "for=127.0.0.1")]
        [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndPort, true, "for=\"127.0.0.1:2\"")]
        [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndRandomPort, true, "for=\"127.0.0.1:_abcdefghi\"")]
        [InlineData("", "::1", 2, NodeFormat.Random, false, "for=_abcdefghi")]
        [InlineData("", "::1", 2, NodeFormat.RandomAndPort, true, "for=\"_abcdefghi:2\"")]
        [InlineData("", "::1", 2, NodeFormat.RandomAndRandomPort, true, "for=\"_abcdefghi:_jklmnopqr\"")]
        [InlineData("existing,header", "::1", 2, NodeFormat.Random, false, "for=_abcdefghi")]
        [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndPort, true, "existing,header|for=\"_abcdefghi:2\"")]
        [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndPort, true, "existing|header|for=\"_abcdefghi:2\"")]
        [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndRandomPort, true, "existing,header|for=\"_abcdefghi:_jklmnopqr\"")]
        [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndRandomPort, true, "existing|header|for=\"_abcdefghi:_jklmnopqr\"")]
        public async Task For_Added(string startValue, string ip, int port, NodeFormat format, bool append, string expected)
        {
            var randomFactory = new TestRandomFactory();
            randomFactory.Instance = new TestRandom() { Sequence = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } };
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = string.IsNullOrEmpty(ip) ? null : IPAddress.Parse(ip);
            httpContext.Connection.RemotePort = port;
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: format,
                byFormat: NodeFormat.None, host: false, proto: false, append);
            await transform.ApplyAsync(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            });
            Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
        }

        [Theory]
        // Using "|" to represent multi-line headers
        [InlineData("", "", 2, NodeFormat.Ip, false, "by=unknown")] // Missing IP falls back to Unknown
        [InlineData("", "", 0, NodeFormat.IpAndPort, true, "by=unknown")] // Missing port excluded
        [InlineData("", "", 2, NodeFormat.IpAndPort, true, "by=\"unknown:2\"")]
        [InlineData("", "", 2, NodeFormat.IpAndRandomPort, true, "by=\"unknown:_abcdefghi\"")]
        [InlineData("", "::1", 2, NodeFormat.Unknown, false, "by=unknown")]
        [InlineData("", "::1", 2, NodeFormat.UnknownAndPort, true, "by=\"unknown:2\"")]
        [InlineData("", "::1", 2, NodeFormat.UnknownAndRandomPort, true, "by=\"unknown:_abcdefghi\"")]
        [InlineData("", "::1", 2, NodeFormat.Ip, false, "by=\"[::1]\"")]
        [InlineData("", "::1", 0, NodeFormat.IpAndPort, true, "by=\"[::1]\"")]
        [InlineData("", "::1", 2, NodeFormat.IpAndPort, true, "by=\"[::1]:2\"")]
        [InlineData("", "::1", 2, NodeFormat.IpAndRandomPort, true, "by=\"[::1]:_abcdefghi\"")]
        [InlineData("", "127.0.0.1", 2, NodeFormat.Ip, false, "by=127.0.0.1")]
        [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndPort, true, "by=\"127.0.0.1:2\"")]
        [InlineData("", "127.0.0.1", 2, NodeFormat.IpAndRandomPort, true, "by=\"127.0.0.1:_abcdefghi\"")]
        [InlineData("", "::1", 2, NodeFormat.Random, false, "by=_abcdefghi")]
        [InlineData("", "::1", 2, NodeFormat.RandomAndPort, true, "by=\"_abcdefghi:2\"")]
        [InlineData("", "::1", 2, NodeFormat.RandomAndRandomPort, true, "by=\"_abcdefghi:_jklmnopqr\"")]
        [InlineData("existing,header", "::1", 2, NodeFormat.Random, false, "by=_abcdefghi")]
        [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndPort, true, "existing,header|by=\"_abcdefghi:2\"")]
        [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndPort, true, "existing|header|by=\"_abcdefghi:2\"")]
        [InlineData("existing,header", "::1", 2, NodeFormat.RandomAndRandomPort, true, "existing,header|by=\"_abcdefghi:_jklmnopqr\"")]
        [InlineData("existing|header", "::1", 2, NodeFormat.RandomAndRandomPort, true, "existing|header|by=\"_abcdefghi:_jklmnopqr\"")]
        public async Task By_Added(string startValue, string ip, int port, NodeFormat format, bool append, string expected)
        {
            var randomFactory = new TestRandomFactory();
            randomFactory.Instance = new TestRandom() { Sequence = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } };
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.LocalIpAddress = string.IsNullOrEmpty(ip) ? null : IPAddress.Parse(ip);
            httpContext.Connection.LocalPort = port;
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderForwardedTransform(randomFactory, forFormat: NodeFormat.None,
                byFormat: format, host: false, proto: false, append);
            await transform.ApplyAsync(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            });
            Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
        }

        [Theory]
        // Using "|" to represent multi-line headers
        [InlineData("", false, "proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
        [InlineData("", true, "proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
        [InlineData("otherHeader", false, "proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
        [InlineData("otherHeader", true, "otherHeader|proto=https;host=\"myHost:80\";for=\"[::1]:10\";by=_abcdefghi")]
        public async Task AllValues_Added(string startValue, bool append, string expected)
        {
            var randomFactory = new TestRandomFactory();
            randomFactory.Instance = new TestRandom() { Sequence = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } };
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("myHost", 80);
            httpContext.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;
            httpContext.Connection.RemotePort = 10;
            var proxyRequest = new HttpRequestMessage();
            proxyRequest.Headers.Add("Forwarded", startValue.Split("|", StringSplitOptions.RemoveEmptyEntries));
            var transform = new RequestHeaderForwardedTransform(randomFactory,
                forFormat: NodeFormat.IpAndPort,
                byFormat: NodeFormat.Random,
                host: true, proto: true, append);
            await transform.ApplyAsync(new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                HeadersCopied = true,
            });
            Assert.Equal(expected.Split("|", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues("Forwarded"));
        }

        internal class TestRandomFactory : IRandomFactory
        {
            internal TestRandom Instance { get; set; }

            public Random CreateRandomInstance()
            {
                return Instance;
            }
        }

        public class TestRandom : Random
        {
            public int[] Sequence { get; set; }
            public int Offset { get; set; }

            public override int Next(int maxValue)
            {
                return Sequence[Offset++];
            }
        }
    }
}
