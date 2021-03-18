// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.Kubernetes.Protocol
{
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

        public List<ProxyRoute> Routes { get; set; }

        public List<Cluster> Cluster { get; set; }
    }
}
