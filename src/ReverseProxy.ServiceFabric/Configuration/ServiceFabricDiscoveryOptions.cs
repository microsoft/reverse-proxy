// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    public sealed class ServiceFabricDiscoveryOptions
    {
        public bool ReportReplicasHealth { get; set; }
        public TimeSpan DiscoveryPeriod { get; set; } = TimeSpan.FromSeconds(30);
    }
}
