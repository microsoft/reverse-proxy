// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry
{
    public enum ProxyStage : int
    {
        SendAsyncStart = 1,
        SendAsyncStop,
        RequestContentTransferStart,
        ResponseContentTransferStart,
        ResponseUpgrade,
    }
}
