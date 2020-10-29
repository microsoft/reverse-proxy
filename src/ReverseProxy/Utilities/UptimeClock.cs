// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Utilities
{
    internal class UptimeClock : IUptimeClock
    {
        public long TickCount => Environment.TickCount64;
    }
}
