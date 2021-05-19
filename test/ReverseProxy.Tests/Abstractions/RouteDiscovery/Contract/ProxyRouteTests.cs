// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Yarp.ReverseProxy.Abstractions.Tests
{
    public class ProxyRouteTests
    {
        [Fact]
        public void Equals_Positive()
        {
            var a = new RouteConfig()
            {
                AuthorizationPolicy = "a",
                ClusterId = "c",
                CorsPolicy = "co",
                Match = new RouteMatch()
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
            var b = new RouteConfig()
            {
                AuthorizationPolicy = "a",
                ClusterId = "c",
                CorsPolicy = "co",
                Match = new RouteMatch()
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

            Assert.True(a.Equals(b));
            Assert.True(a.Equals(c));
        }

        [Fact]
        public void Equals_Negative()
        {
            var a = new RouteConfig()
            {
                AuthorizationPolicy = "a",
                ClusterId = "c",
                CorsPolicy = "co",
                Match = new RouteMatch()
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
            var e = a with { Match = new RouteMatch() };
            var f = a with { Metadata = new Dictionary<string, string>() { { "f", "f1" } } };
            var g = a with { Order = null };
            var h = a with { RouteId = "h" };

            Assert.False(a.Equals(b));
            Assert.False(a.Equals(c));
            Assert.False(a.Equals(d));
            Assert.False(a.Equals(e));
            Assert.False(a.Equals(f));
            Assert.False(a.Equals(g));
            Assert.False(a.Equals(h));
        }

        [Fact]
        public void Equals_Null_False()
        {
            Assert.False(new RouteConfig().Equals(null));
        }

        [Fact]
        public void RouteConfig_CanBeJsonSerialized()
        {
            var route1 = new RouteConfig()
            {
                AuthorizationPolicy = "a",
                ClusterId = "c",
                CorsPolicy = "co",
                Match = new RouteMatch()
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
                Transforms = new[]
                {
                    new Dictionary<string, string>
                    {
                        { "key", "value" },
                        { "key1", "" }
                    }
                },
                Order = 1,
                RouteId = "R",
            };

            var json = JsonSerializer.Serialize(route1);
            var route2 = JsonSerializer.Deserialize<RouteConfig>(json);

            Assert.Equal(route1, route2);
        }
    }
}
