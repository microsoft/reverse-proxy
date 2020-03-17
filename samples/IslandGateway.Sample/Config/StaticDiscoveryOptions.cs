// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using IslandGateway.Core.Abstractions;

namespace IslandGateway.Sample.Config
{
    internal class StaticDiscoveryOptions
    {
        public IList<Backend> Backends { get; } = new List<Backend>();
        public IDictionary<string, IList<BackendEndpoint>> Endpoints { get; } = new Dictionary<string, IList<BackendEndpoint>>(StringComparer.Ordinal);
        public IList<GatewayRoute> Routes { get; } = new List<GatewayRoute>();
    }
}
