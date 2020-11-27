// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Sockets;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface ISocketsTelemetryConsumer
    {
        void OnConnectStart(DateTime timestamp, string address);

        void OnConnectStop(DateTime timestamp);

        void OnConnectFailed(DateTime timestamp, SocketError error, string exceptionMessage);
    }
}
