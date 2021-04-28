// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.Service
{
    /// <summary>
    /// Provides methods to validate routes and clusters.
    /// </summary>
    public interface IConfigValidator
    {
        /// <summary>
        /// Validates a route and returns all errors
        /// </summary>
        ValueTask<IList<Exception>> ValidateRouteAsync(RouteConfig route);

        /// <summary>
        /// Validates a cluster and returns all errors.
        /// </summary>
        ValueTask<IList<Exception>> ValidateClusterAsync(Cluster cluster);
    }
}
