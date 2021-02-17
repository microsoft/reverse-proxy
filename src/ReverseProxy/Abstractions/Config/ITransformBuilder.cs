// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// Validates and builds request and response transforms for a given route.
    /// </summary>
    public interface ITransformBuilder
    {
        /// <summary>
        /// Validates that each transform for the given route is known and has the expected parameters. All transforms are validated
        /// so all errors can be reported.
        /// </summary>
        IReadOnlyList<Exception> ValidateRoute(ProxyRoute route);

        /// <summary>
        /// Validates that any cluster data needed for transforms is valid.
        /// </summary>
        IReadOnlyList<Exception> ValidateCluster(Cluster cluster);

        /// <summary>
        /// Builds the transforms for the given route into executable rules.
        /// </summary>
        HttpTransformer Build(ProxyRoute route, Cluster cluster);
    }
}
