// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Protocol;

public enum MessageType
{
    Heartbeat,
    Update,
    Remove,
}

public struct Message
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MessageType MessageType { get; set; }

    public string Key { get; set; }

    public List<RouteConfig> Routes { get; set; }

    public List<ClusterConfig> Cluster { get; set; }
}
