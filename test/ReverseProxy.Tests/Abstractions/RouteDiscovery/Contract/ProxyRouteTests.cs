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
        public void DeepClone_Works()
        {
            var sut = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Methods = new[] { "GET", "POST" },
                    Hosts = new[] { "example.com" },
                    Path = "/",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1", "value2" },
                            Mode = HeaderMatchMode.HeaderPrefix,
                            IsCaseSensitive = true,
                        }
                    },
                },
                Order = 2,
                ClusterId = "cluster1",
                AuthorizationPolicy = "policy1",
                CorsPolicy = "policy2",
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            var clone = sut.DeepClone();

            Assert.NotSame(sut, clone);
            Assert.Equal(sut.RouteId, clone.RouteId);
            Assert.NotSame(sut.Match, clone.Match);
            Assert.NotSame(sut.Match.Methods, clone.Match.Methods);
            Assert.NotSame(sut.Match.Hosts, clone.Match.Hosts);
            Assert.NotSame(sut.Match.Headers, clone.Match.Headers);
            Assert.Equal(sut.Match.Methods, clone.Match.Methods);
            Assert.Equal(sut.Match.Hosts, clone.Match.Hosts);
            Assert.Equal(sut.Match.Headers.Count, clone.Match.Headers.Count); // These types don't implement the standard Object.Equals
            Assert.Equal(sut.Match.Path, clone.Match.Path);
            Assert.Equal(sut.Order, clone.Order);
            Assert.Equal(sut.ClusterId, clone.ClusterId);
            Assert.Equal(sut.AuthorizationPolicy, clone.AuthorizationPolicy);
            Assert.Equal(sut.CorsPolicy, clone.CorsPolicy);
            Assert.NotNull(clone.Metadata);
            Assert.NotSame(sut.Metadata, clone.Metadata);
            Assert.Equal("value", clone.Metadata["key"]);

            Assert.True(ProxyRoute.Equals(sut, clone));
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            var sut = new ProxyRoute();

            var clone = sut.DeepClone();

            Assert.NotSame(sut, clone);
            Assert.Null(clone.RouteId);
            Assert.Null(clone.Match.Methods);
            Assert.Null(clone.Match.Hosts);
            Assert.Null(clone.Match.Headers);
            Assert.Null(clone.Match.Path);
            Assert.Null(clone.Order);
            Assert.Null(clone.ClusterId);
            Assert.Null(clone.AuthorizationPolicy);
            Assert.Null(clone.CorsPolicy);
            Assert.Null(clone.Metadata);

            Assert.True(ProxyRoute.Equals(sut, clone));
        }
    }
}
