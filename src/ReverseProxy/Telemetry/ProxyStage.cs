// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry
{
    public enum ProxyStage : int
    {
        InvokerSendAsyncStart = 1,
        InvokerSendAsyncStop,
        RequestContentTransferStart,
        RequestContentTransferStop,
        ResponseContentTransferStart,
        ResponseContentTransferStop,
        ResponseUpgradeStart,
    }
}
