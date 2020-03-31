// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;

namespace Microsoft.ReverseProxy.Sample.Config
{
    internal class StaticDiscoveryOptions
    {
        public IList<Backend> Backends { get; } = new List<Backend>();
        public IDictionary<string, IList<BackendEndpoint>> Endpoints { get; } = new Dictionary<string, IList<BackendEndpoint>>(StringComparer.Ordinal);
        public IList<ProxyRoute> Routes { get; } = new List<ProxyRoute>();
    }
}
