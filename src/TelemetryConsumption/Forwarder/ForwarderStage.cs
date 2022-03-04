// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// Stages of forwarding a request.
/// </summary>
public enum ForwarderStage : int
{
    SendAsyncStart = 1,
    SendAsyncStop,
    RequestContentTransferStart,
    ResponseContentTransferStart,
    ResponseUpgrade,
}
