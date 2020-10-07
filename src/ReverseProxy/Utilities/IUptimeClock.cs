// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Measures the time passed since the application start.
    /// </summary>
    public interface IUptimeClock
    {
        long TickCount { get; }
    }
}
