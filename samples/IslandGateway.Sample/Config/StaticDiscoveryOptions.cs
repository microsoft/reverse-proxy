// <copyright file="StaticDiscoveryOptions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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
