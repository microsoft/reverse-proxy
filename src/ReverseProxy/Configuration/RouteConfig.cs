// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration
{
    /// <summary>
    /// Describes a route that matches incoming requests based on a the <see cref="Match"/> criteria
    /// and proxies matching requests to the cluster identified by its <see cref="ClusterId"/>.
    /// </summary>
    public sealed record RouteConfig
    {
        /// <summary>
        /// Globally unique identifier of the route.
        /// This field is required.
        /// </summary>
        public string RouteId { get; init; } = default!;

        /// <summary>
        /// Parameters used to match requests.
        /// This field is required.
        /// </summary>
        public RouteMatch Match { get; init; } = default!;

        /// <summary>
        /// Optionally, an order value for this route. Routes with lower numbers take precedence over higher numbers.
        /// </summary>
        public int? Order { get; init; }

        /// <summary>
        /// Gets or sets the cluster that requests matching this route
        /// should be proxied to.
        /// </summary>
        public string? ClusterId { get; init; }

        /// <summary>
        /// The name of the AuthorizationPolicy to apply to this route.
        /// If not set then only the FallbackPolicy will apply.
        /// Set to "Default" to enable authorization with the applications default policy.
        /// Set to "Anonymous" to disable all authorization checks for this route.
        /// </summary>
        public string? AuthorizationPolicy { get; init; }

        /// <summary>
        /// The name of the CorsPolicy to apply to this route.
        /// If not set then the route won't be automatically matched for cors preflight requests.
        /// Set to "Default" to enable cors with the default policy.
        /// Set to "Disable" to refuses cors requests for this route.
        /// </summary>
        public string? CorsPolicy { get; init; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this route.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }

        /// <summary>
        /// Parameters used to transform the request and response. See <see cref="Transforms.Builder.ITransformBuilder"/>.
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<string, string>>? Transforms { get; init; }

        public bool Equals(RouteConfig? other)
        {
            if (other == null)
            {
                return false;
            }

            return Order == other.Order
                && string.Equals(RouteId, other.RouteId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ClusterId, other.ClusterId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(AuthorizationPolicy, other.AuthorizationPolicy, StringComparison.OrdinalIgnoreCase)
                && string.Equals(CorsPolicy, other.CorsPolicy, StringComparison.OrdinalIgnoreCase)
                && Match == other.Match
                && CaseInsensitiveEqualHelper.Equals(Metadata, other.Metadata)
                && CaseInsensitiveEqualHelper.Equals(Transforms, other.Transforms);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Order,
                RouteId?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                ClusterId?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                AuthorizationPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                CorsPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                Match,
                Metadata,
                Transforms);
        }
    }
}
