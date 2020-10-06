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
                Name = "header1",
                Values = new[] { "value1", "value2" },
                Mode = HeaderMatchMode.HeaderPrefix,
                IsCaseSensitive = true,
            };

            var clone = sut.DeepClone();

            Assert.NotSame(sut, clone);
            Assert.Equal(sut.Name, clone.Name);
            Assert.NotSame(sut.Values, clone.Values);
            Assert.Equal(sut.Values, clone.Values);
            Assert.Equal(sut.Mode, clone.Mode);
            Assert.Equal(sut.IsCaseSensitive, clone.IsCaseSensitive);

            Assert.True(RouteHeader.Equals(sut, clone));
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            var sut = new RouteHeader();
            var clone = sut.DeepClone();

            Assert.NotSame(sut, clone);
            Assert.Null(clone.Name);
            Assert.Null(clone.Values);
            Assert.Equal(HeaderMatchMode.ExactHeader, clone.Mode);
            Assert.False(clone.IsCaseSensitive);

            Assert.True(RouteHeader.Equals(sut, clone));
        }
    }
}
