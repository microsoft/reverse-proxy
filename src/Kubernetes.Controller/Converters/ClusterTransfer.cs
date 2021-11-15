// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Controller.Services;

internal sealed class ClusterTransfer
{
    public Dictionary<string, DestinationConfig> Destinations { get; set; } = new Dictionary<string, DestinationConfig>();
    public string ClusterId { get; set; }
}
