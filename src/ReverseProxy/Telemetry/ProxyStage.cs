// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry
{
    internal enum ProxyStage : int
    {
        SendAsyncStart = 1,
        SendAsyncStop,
        RequestContentTransferStart,
        RequestContentTransferStop,
        ResponseContentTransferStart,
        ResponseContentTransferStop,
        ResponseUpgrade,
    }
}
