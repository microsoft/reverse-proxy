// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Authentication;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface INetSecurityTelemetryConsumer
    {
        void OnHandshakeStart(DateTime timestamp, bool isServer, string targetHost);

        void OnHandshakeStop(DateTime timestamp, SslProtocols protocol);

        void OnHandshakeFailed(DateTime timestamp, bool isServer, TimeSpan elapsed, string exceptionMessage);
    }
}
