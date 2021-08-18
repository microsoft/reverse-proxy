// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// Stages of forwarding a request.
    /// </summary>
    public enum ForwarderStage
    {
        SendAsyncStart = 1,
        SendAsyncStop,
        RequestContentTransferStart,
        ResponseContentTransferStart,
        ResponseUpgrade,
    }
}
