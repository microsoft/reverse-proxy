// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface IKestrelTelemetryConsumer
    {
#if NET5_0
        void OnRequestStart(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method);

        void OnRequestStop(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method);
#else
        void OnRequestStart(DateTime timestamp, string connectionId, string requestId);

        void OnRequestStop(DateTime timestamp, string connectionId, string requestId);
#endif
    }
}
