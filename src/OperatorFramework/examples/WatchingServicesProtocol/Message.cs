// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WatchingServicesProtocol
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

        public List<Rule> Rules { get; set; }
    }

    public struct Rule
    {
        public string Host { get; set; }
        public string Path { get; set; }
        public int Port { get; set; }
        public List<string> Ready { get; set; }
        public List<string> NotReady { get; set; }
    }
}
