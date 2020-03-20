// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Describes a route that matches incoming requests based on a <see cref="Methods"/>, <see cref="Host"/>, and <see cref="Path"/>
    /// and proxies matching requests to the backend identified by its <see cref="BackendId"/>.
    /// </summary>
    public sealed class GatewayRoute : IDeepCloneable<GatewayRoute>
    {
        /// <summary>
        /// Globally unique identifier of the route.
        /// </summary>
        public string RouteId { get; set; }

        /// <summary>
        /// Only match requests that use these optional HTTP methods. E.g. GET, POST.
        /// </summary>
        public string[] Methods { get; set; }

        /// <summary>
        /// Only match requests with the given Host header.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Only match requests with the given Path pattern.
        /// </summary>
        public string Path { get; set; }

        // TODO:
        /// <summary>
        /// Only match requests that contain all of these query parameters.
        /// </summary>
        // public ICollection<KeyValuePair<string, string>> QueryParameters { get; set; }

        // TODO:
        /// <summary>
        /// Only match requests that contain all of these request headers.
        /// </summary>
        // public ICollection<KeyValuePair<string, string>> Headers { get; set; }

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
                Methods = (string[])Methods?.Clone(),
                Host = Host,
                Path = Path,
                // Headers = Headers.DeepClone(); // TODO:
                Priority = Priority,
                BackendId = BackendId,
                Metadata = Metadata?.DeepClone(StringComparer.Ordinal),
            };
        }
    }
}
