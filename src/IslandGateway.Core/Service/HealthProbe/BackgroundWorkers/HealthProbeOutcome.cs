// <copyright file="HealthProbeOutcome.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.Service.HealthProbe
{
    // The outcome of probing the endpoint health. The class enumerate the below possible state.
    // This is also offering help on logging.
    internal enum HealthProbeOutcome
    {
        Unknown,
        Success,
        TransportFailure,
        HttpFailure,
        Timeout,
        Canceled,
    }
}