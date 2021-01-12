// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Service.Routing;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class RouteHeaderTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new RouteHeader();
        }

        [Fact]
        public void Equals_Positive()
        {
            var a = new RouteHeader()
            {
                Name = "foo",
                Mode = HeaderMatchMode.Exists,
                Values = new[] { "v1", "v2" },
                IsCaseSensitive = true,
            };
            var b = new RouteHeader()
            {
                Name = "foo",
                Mode = HeaderMatchMode.Exists,
                Values = new[] { "v1", "v2" },
                IsCaseSensitive = true,
            };
            var c = a with { }; // Clone
            Assert.True(RouteHeader.Equals(a, b));
            Assert.True(RouteHeader.Equals(a, c));
        }

        [Fact]
        public void Equals_Negative()
        {
            var a = new RouteHeader()
            {
                Name = "foo",
                Mode = HeaderMatchMode.Exists,
                Values = new[] { "v1", "v2" },
                IsCaseSensitive = true,
            };
            var b = a with { Name = "bar" };
            var c = a with { Mode = HeaderMatchMode.ExactHeader };
            var d = a with { Values = new[] { "v1", "v3" } };
            var e = a with { IsCaseSensitive = false };
            Assert.False(RouteHeader.Equals(a, b));
            Assert.False(RouteHeader.Equals(a, c));
            Assert.False(RouteHeader.Equals(a, d));
            Assert.False(RouteHeader.Equals(a, e));
        }
    }
}
