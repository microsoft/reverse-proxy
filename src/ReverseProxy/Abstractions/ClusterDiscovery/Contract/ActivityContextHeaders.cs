// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Abstractions
{
    [Flags]
    public enum ActivityContextHeaders
    {
        None = 0,
        Baggage = 1,
        CorrelationContext = 2,
        BaggageAndCorrelationContext = Baggage | CorrelationContext
    }
}
