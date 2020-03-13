// <copyright file="GatewayRoute.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Describes a route that matches incoming requests based on a <see cref="Rule"/>
    /// and proxies matching requests to the backend identified by its <see cref="BackendId"/>.
    /// </summary>
    public sealed class GatewayRoute : IDeepCloneable<GatewayRoute>
    {
        /// <summary>
        /// Globally unique identifier of the route.
        /// </summary>
        public string RouteId { get; set; }

        /// <summary>
        /// Rule that incoming requests must match for this route to apply. E.g. <c>Host('example.com')</c>.
        /// </summary>
        public string Rule { get; set; }

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
                Rule = Rule,
                Priority = Priority,
                BackendId = BackendId,
                Metadata = Metadata?.DeepClone(StringComparer.Ordinal),
            };
        }
    }
}
