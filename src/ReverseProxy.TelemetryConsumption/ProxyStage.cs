// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// Stages of proxying a request.
    /// </summary>
    public enum ProxyStage : int
    {
        SendAsyncStart = 1,
        SendAsyncStop,
        RequestContentTransferStart,
        ResponseContentTransferStart,
        ResponseUpgrade,
    }
}
