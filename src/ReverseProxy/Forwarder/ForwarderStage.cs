// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Forwarder
{
    internal enum ForwarderStage
    {
        SendAsyncStart = 1,
        SendAsyncStop,
        RequestContentTransferStart,
        ResponseContentTransferStart,
        ResponseUpgrade,
    }
}
