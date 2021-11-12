// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class ActiveHealthCheckConfigTests
{
    [Fact]
    public void Equals_Same_Value_Returns_True()
    {
        var options1 = new ActiveHealthCheckConfig
        {
            Enabled = true,
            Interval = TimeSpan.FromSeconds(2),
            Timeout = TimeSpan.FromSeconds(1),
            Policy = "Any5xxResponse",
            Path = "/a",
        };

        var options2 = new ActiveHealthCheckConfig
        {
            Enabled = true,
            Interval = TimeSpan.FromSeconds(2),
            Timeout = TimeSpan.FromSeconds(1),
            Policy = "any5xxResponse",
            Path = "/a",
        };

        var equals = options1.Equals(options2);

        Assert.True(equals);
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    [Fact]
    public void Equals_Different_Value_Returns_False()
    {
        var options1 = new ActiveHealthCheckConfig
        {
            Enabled = true,
            Interval = TimeSpan.FromSeconds(2),
            Timeout = TimeSpan.FromSeconds(1),
            Policy = "Any5xxResponse",
            Path = "/a",
        };

        var options2 = new ActiveHealthCheckConfig
        {
            Enabled = false,
            Interval = TimeSpan.FromSeconds(4),
            Timeout = TimeSpan.FromSeconds(2),
            Policy = "AnyFailure",
            Path = "/b",
        };

        var equals = options1.Equals(options2);

        Assert.False(equals);
    }

    [Fact]
    public void Equals_Second_Null_Returns_False()
    {
        var options1 = new ActiveHealthCheckConfig();

        var equals = options1.Equals(null);

        Assert.False(equals);
    }
}
