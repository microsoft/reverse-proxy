// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// Provides a method that uses configuration repositories to build a <see cref="DynamicConfigRoot"/> object.
    /// </summary>
    internal interface IDynamicConfigBuilder
    {
        /// <summary>
        /// Creates a <see cref="DynamicConfigRoot"/> object representing the current desired proxy dynamic configurations.
        /// </summary>
        Task<DynamicConfigRoot> BuildConfigAsync(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters, CancellationToken cancellation);
    }
}
