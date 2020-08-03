// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    public interface IProxyConfig
    {
        IReadOnlyList<ProxyRoute> Routes { get; }
        IReadOnlyList<Cluster> Clusters { get; }

        IChangeToken ChangeToken { get; }
    }
}
