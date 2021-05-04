// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Abstractions;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Kubernetes.Controller.Services
{
    internal sealed class ClusterTrasfer
    {
        public Dictionary<string, DestinationConfig> Destinations { get; set; } = new Dictionary<string, DestinationConfig>();
        public string ClusterId { get; set; }
    }
}
