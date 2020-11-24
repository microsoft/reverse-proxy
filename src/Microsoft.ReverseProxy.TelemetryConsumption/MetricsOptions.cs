// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    internal static class MetricsOptions
    {
        // TODO: Should this be publicly configurable? It's currently only visible to tests to reduce execution time
        public static float IntervalSeconds { get; set; } = 1;
    }
}
