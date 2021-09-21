// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.Telemetry.Consumption
{
    /// <summary>
    /// The reason the WebSocket connection closed.
    /// </summary>
    public enum WebSocketCloseReason : int
    {
        Unknown,
        ClientGracefulClose,
        ServerGracefulClose,
        ClientDisconnect,
        ServerDisconnect,
    }
}
