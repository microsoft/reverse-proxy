// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Telemetry
{
    internal enum ProxyStage : int
    {
        SendAsyncStart = 1,
        SendAsyncStop,
        RequestContentTransferStart,
        ResponseContentTransferStart,
        ResponseUpgrade,
    }
}
