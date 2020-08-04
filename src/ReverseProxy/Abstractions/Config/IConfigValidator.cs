// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// Provides a method to validate routes and clusters.
    /// </summary>
    public interface IConfigValidator
    {
        /// <summary>
        /// Validates a route and returns all errors
        /// </summary>
        Task<IList<Exception>> ValidateRouteAsync(ProxyRoute route);

        /// <summary>
        /// Validates a cluster and returns all errors.
        /// </summary>
        Task<IList<Exception>> ValidateClusterAsync(Cluster cluster);
    }
}
