// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.Routing;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class ProxyRouteTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new ProxyRoute();
        }

        [Fact]
        public void Equals_Positive()
        {
            var a = new ProxyRoute()
            {
                AuthorizationPolicy = "a",
                ClusterId = "c",
                CorsPolicy = "co",
                Match = new ProxyMatch()
                {
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "Hi",
                            Values = new[] { "v1", "v2" },
                            IsCaseSensitive = true,
                            Mode = HeaderMatchMode.HeaderPrefix,
                        }
                    },
                    Hosts = new[] { "foo:90" },
                    Methods = new[] { "GET", "POST" },
                    Path = "/p",
                },
                Metadata = new Dictionary<string, string>()
                {
                    { "m", "m1" }
                },
                Order = 1,
                RouteId = "R",
            };
            var b = new ProxyRoute()
            {
                AuthorizationPolicy = "a",
                ClusterId = "c",
                CorsPolicy = "co",
                Match = new ProxyMatch()
                {
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "Hi",
                            Values = new[] { "v1", "v2" },
                            IsCaseSensitive = true,
                            Mode = HeaderMatchMode.HeaderPrefix,
                        }
                    },
                    Hosts = new[] { "foo:90" },
                    Methods = new[] { "GET", "POST" },
                    Path = "/p"
                },
                Metadata = new Dictionary<string, string>()
                {
                    { "m", "m1" }
                },
                Order = 1,
                RouteId = "R",
            };
            var c = b with { }; // Clone

            Assert.True(ProxyRoute.Equals(a, b));
            Assert.True(ProxyRoute.Equals(a, c));
        }

        [Fact]
        public void Equals_Negative()
        {
            var a = new ProxyRoute()
            {
                AuthorizationPolicy = "a",
                ClusterId = "c",
                CorsPolicy = "co",
                Match = new ProxyMatch()
                {
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "Hi",
                            Values = new[] { "v1", "v2" },
                            IsCaseSensitive = true,
                            Mode = HeaderMatchMode.HeaderPrefix,
                        }
                    },
                    Hosts = new[] { "foo:90" },
                    Methods = new[] { "GET", "POST" },
                    Path = "/p",
                },
                Metadata = new Dictionary<string, string>()
                {
                    { "m", "m1" }
                },
                Order = 1,
                RouteId = "R",
            };
            var b = a with { AuthorizationPolicy = "b" };
            var c = a with { ClusterId = "d" };
            var d = a with { CorsPolicy = "p" };
            var e = a with { Match = new ProxyMatch() };
            var f = a with { Metadata = new Dictionary<string, string>() { { "f", "f1" } } };
            var g = a with { Order = null };
            var h = a with { RouteId = "h" };

            Assert.False(ProxyRoute.Equals(a, b));
            Assert.False(ProxyRoute.Equals(a, c));
            Assert.False(ProxyRoute.Equals(a, d));
            Assert.False(ProxyRoute.Equals(a, e));
            Assert.False(ProxyRoute.Equals(a, f));
            Assert.False(ProxyRoute.Equals(a, g));
            Assert.False(ProxyRoute.Equals(a, h));
        }
    }
}
