// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Abstractions;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Kubernetes.Controller.Services
{
    internal class ClusterTrasfer
    {
        public Dictionary<string, Destination> Destinations { get; set; } = new Dictionary<string, Destination>();
        public string ClusterId { get; set; }
    }
}
