// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;

using FluentAssertions;

using Microsoft.ReverseProxy.Common.Util;

using Xunit;

namespace Microsoft.ReverseProxy.Common.Tests
{
    public class TimeSpanIso8601Tests
    {
        [Theory]
        [InlineData("P0Y0M0DT0H0M0S", 0, 0, 0, 0)]
        [InlineData("PT0S", 0, 0, 0, 0)]
        [InlineData("P0Y0M0DT0H0M1S", 0, 0, 0, 1)]
        [InlineData("PT0H1S", 0, 0, 0, 1)]
        [InlineData("PT5S", 0, 0, 0, 5)]
        [InlineData("P1D", 1, 0, 0, 0)]
        [InlineData("P1DT1M", 1, 0, 1, 0)]
        [InlineData("P1Y", 365, 0, 0, 0)]
        public void Constructor_ParsesStringCorrectly(string iso8601String, int expectedDays, int expectedHours, int expectedMinutes, int expectedSeconds)
        {
            // Act
            var timeSpan = new TimeSpanIso8601(iso8601String);

            // Assert
            timeSpan.Value.Should().Be(new TimeSpan(expectedDays, expectedHours, expectedMinutes, expectedSeconds));
        }
    }
}
