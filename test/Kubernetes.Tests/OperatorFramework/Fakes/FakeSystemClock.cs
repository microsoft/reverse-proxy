// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Internal;
using System;

namespace Yarp.Kubernetes.OperatorFramework.Fakes;

public class FakeSystemClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2020, 10, 14, 12, 34, 56, TimeSpan.Zero);

    public void Advance(TimeSpan timeSpan)
    {
        UtcNow += timeSpan;
    }
}
