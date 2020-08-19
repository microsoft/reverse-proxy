// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    internal static class EventIds
    {
        public static readonly EventId LoadData = new EventId(1, "ApplyProxyConfig");
        public static readonly EventId ErrorSignalingChange = new EventId(2, "ApplyProxyConfigFailed");
    }
}
