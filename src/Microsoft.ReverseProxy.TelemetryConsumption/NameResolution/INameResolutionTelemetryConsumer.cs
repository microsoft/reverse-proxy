// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface INameResolutionTelemetryConsumer
    {
        void OnResolutionStart(DateTime timestamp, string hostNameOrAddress);

        void OnResolutionStop(DateTime timestamp);

        void OnResolutionFailed(DateTime timestamp);
    }
}
