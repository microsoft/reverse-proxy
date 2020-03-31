// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Describes a route that matches incoming requests based on a the <see cref="Match"/> criteria
    /// and proxies matching requests to the backend identified by its <see cref="BackendId"/>.
    /// </summary>
    public sealed class GatewayRoute : IDeepCloneable<GatewayRoute>
    {
        /// <summary>
        /// Globally unique identifier of the route.
        /// </summary>
        public string RouteId { get; set; }

        public GatewayMatch Match { get; private set; } = new GatewayMatch();

        /// <summary>
        /// Optionally, a priority value for this route. Routes with higher numbers take precedence over lower numbers.
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// Gets or sets the backend that requests matching this route
        /// should be proxied to.
        /// </summary>
        public string BackendId { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this route.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <inheritdoc/>
        GatewayRoute IDeepCloneable<GatewayRoute>.DeepClone()
        {
            return new GatewayRoute
            {
                RouteId = RouteId,
                Match = Match.DeepClone(),
                Priority = Priority,
                BackendId = BackendId,
                Metadata = Metadata?.DeepClone(StringComparer.Ordinal),
            };
        }
    }
}
