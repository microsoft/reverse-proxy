// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Yarp.ReverseProxy.Discovery.Tests
{
    public class RouteHeaderTests
    {
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
            Assert.True(a.Equals(b));
            Assert.True(a.Equals(c));
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
            Assert.False(a.Equals(b));
            Assert.False(a.Equals(c));
            Assert.False(a.Equals(d));
            Assert.False(a.Equals(e));
        }

        [Fact]
        public void Equals_Null_False()
        {
            Assert.False(new RouteHeader().Equals(null));
        }
    }
}
