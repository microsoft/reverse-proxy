// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace Microsoft.ReverseProxy.ServiceFabric
{
    internal sealed class ServiceFabricServiceDiscoveryOptions
    {
        internal bool ReportReplicasHealth { get; set; } = false;
        internal TimeSpan DiscoveryPeriod { get; set; } = TimeSpan.FromSeconds(30);
    }
}
