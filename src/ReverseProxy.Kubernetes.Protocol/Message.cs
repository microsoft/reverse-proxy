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

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct Message
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MessageType MessageType { get; set; }

        public string Key { get; set; }

#pragma warning disable CA2227 // Collection properties should be read only
        public List<RouteConfig> Routes { get; set; }

        public List<Cluster> Cluster { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
