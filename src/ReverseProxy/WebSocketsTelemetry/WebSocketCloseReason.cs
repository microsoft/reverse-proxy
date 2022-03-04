// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

internal enum WebSocketCloseReason : int
{
    Unknown,
    ClientGracefulClose,
    ServerGracefulClose,
    ClientDisconnect,
    ServerDisconnect,
}
