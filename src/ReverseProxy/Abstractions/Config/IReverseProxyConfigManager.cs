// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// High-level management of Reverse Proxy state.
    /// </summary>
    public interface IReverseProxyConfigManager
    {
        /// <summary>
        /// Applies latest configurations obtained from <see cref="IDynamicConfigBuilder"/>.
        /// </summary>
        Task ApplyConfigurationsAsync(IList<ProxyRoute> routes, IDictionary<string, Cluster> clusters, CancellationToken cancellation);
    }
}
