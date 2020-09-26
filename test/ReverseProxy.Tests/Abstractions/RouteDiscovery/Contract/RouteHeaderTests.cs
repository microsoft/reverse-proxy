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
        public void DeepClone_Works()
        {
            var sut = new RouteHeader
            {
                HeaderName = "header1",
                HeaderValues = new[] { "value1", "value2" },
                Mode = HeaderMatchMode.Prefix,
                CaseSensitive = true,
            };

            var clone = sut.DeepClone();

            Assert.NotSame(sut, clone);
            Assert.Equal(sut.HeaderName, clone.HeaderName);
            Assert.NotSame(sut.HeaderValues, clone.HeaderValues);
            Assert.Equal(sut.HeaderValues, clone.HeaderValues);
            Assert.Equal(sut.Mode, clone.Mode);
            Assert.Equal(sut.CaseSensitive, clone.CaseSensitive);

            Assert.True(RouteHeader.Equals(sut, clone));
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            var sut = new RouteHeader();
            var clone = sut.DeepClone();

            Assert.NotSame(sut, clone);
            Assert.Null(clone.HeaderName);
            Assert.Null(clone.HeaderValues);
            Assert.Equal(HeaderMatchMode.Exact, clone.Mode);
            Assert.False(clone.CaseSensitive);

            Assert.True(RouteHeader.Equals(sut, clone));
        }
    }
}
