// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.ServiceFabric;

public sealed class ServiceFabricDiscoveryOptions
{
    public bool ReportReplicasHealth { get; set; }
    public TimeSpan DiscoveryPeriod { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to allow the Reverse Proxy to complete initialization
    /// before Service Fabric Discovery has executed successfully once.
    /// If set to true, then the proxy may start up in a potentially undesired configuration
    /// (i.e. no routes and no clusters).
    /// </summary>
    public bool AllowStartBeforeDiscovery { get; set; }

    /// <summary>
    /// Whether to discover unencrypted HTTP (i.e. non-HTTPS) endpoints.
    /// Defaults to <c>false</c>, so that only HTTPS endpoints are allowed.
    /// </summary>
    public bool DiscoverInsecureHttpDestinations { get; set; }

    public bool AlwaysSendImmediateHealthReports { get; set; }
}
