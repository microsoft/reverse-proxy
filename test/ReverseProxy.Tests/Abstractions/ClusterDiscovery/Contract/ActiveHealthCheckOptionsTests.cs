// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class ActiveHealthCheckOptionsTests
    {
        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new ActiveHealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "Any5xxResponse",
                Path = "/a",
            };

            var options2 = new ActiveHealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "Any5xxResponse",
                Path = "/a",
            };

            var equals = ActiveHealthCheckOptions.Equals(options1, options2);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            var options1 = new ActiveHealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "Any5xxResponse",
                Path = "/a",
            };

            var options2 = new ActiveHealthCheckOptions
            {
                Enabled = false,
                Interval = TimeSpan.FromSeconds(4),
                Timeout = TimeSpan.FromSeconds(2),
                Policy = "AnyFailure",
                Path = "/b",
            };

            var equals = ActiveHealthCheckOptions.Equals(options1, options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            var options2 = new ActiveHealthCheckOptions
            {
                Enabled = false,
                Interval = TimeSpan.FromSeconds(4),
                Timeout = TimeSpan.FromSeconds(2),
                Policy = "Any5xxResponse",
                Path = "/b",
            };

            var equals = ActiveHealthCheckOptions.Equals(null, options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new ActiveHealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "Any5xxResponse",
                Path = "/a",
            };

            var equals = ActiveHealthCheckOptions.Equals(options1, null);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            var equals = ActiveHealthCheckOptions.Equals(null, null);

            Assert.True(equals);
        }
    }
}
