// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Xunit;

namespace Yarp.ReverseProxy;

public class TelemetryEnumTests
{
    [Theory]
    [InlineData(typeof(Telemetry.Consumption.ForwarderStage), typeof(Forwarder.ForwarderStage))]
    [InlineData(typeof(Telemetry.Consumption.WebSocketCloseReason), typeof(WebSocketsTelemetry.WebSocketCloseReason))]
    public void ExposedEnumsMatchInternalCopies(Type publicEnum, Type internalEnum)
    {
        Assert.Equal(internalEnum.GetEnumNames(), publicEnum.GetEnumNames());
        Assert.Equal(internalEnum.GetEnumValues().Cast<int>().ToArray(), publicEnum.GetEnumValues().Cast<int>().ToArray());
    }
}
