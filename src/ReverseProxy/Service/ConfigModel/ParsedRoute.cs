// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.ReverseProxy.ConfigModel
{
    // TODO: Do we even need the ParsedRoute? It now matches the ProxyRoute 1:1
    internal class ParsedRoute
    {
        /// <summary>
        /// Unique identifier of this route.
        /// </summary>
        public string RouteId { get; set; }

        /// <summary>
        /// Only match requests that use these optional HTTP methods. E.g. GET, POST.
        /// </summary>
        public IReadOnlyList<string> Methods { get; set; }

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
        /// Gets or sets the priority of this route.
        /// Routes with higher priority are evaluated first.
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// Gets or sets the cluster that requests matching this route
        /// should be proxied to.
        /// </summary>
        public string ClusterId { get; set; }

        /// <summary>
        /// The name of the AuthorizationPolicy to apply to this route.
        /// If not set then only the FallbackPolicy will apply.
        /// Set to "Default" to enable authorization with the applications default policy.
        /// Set to "Anonymous" to disable all authorization checks for this route.
        /// </summary>
        public string AuthorizationPolicy { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this route.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// Parameters used to transform the request and response. See <see cref="ITransformBuilder"/>.
        /// </summary>
        public IList<IDictionary<string, string>> Transforms { get; set; }

        /// <summary>
        /// The name of the CorsPolicy to apply to this route.
        /// If not set then the route won't be automatically matched for cors preflight requests.
        /// Set to "Default" to enable cors with the default policy.
        /// Set to "Disable" to refuses cors requests for this route.
        /// </summary>
        public string CorsPolicy { get; set; }

        // Used to diff for config changes
        internal int GetConfigHash()
        {
            var hash = 0;

            if (!string.IsNullOrEmpty(RouteId))
            {
                hash ^= RouteId.GetHashCode();
            }

            if (Methods != null && Methods.Count > 0)
            {
                // Assumes un-ordered
                hash ^= Methods.Select(item => item.GetHashCode())
                    .Aggregate((total, nextCode) => total ^ nextCode);
            }

            if (!string.IsNullOrEmpty(Host))
            {
                hash ^= Host.GetHashCode();
            }

            if (!string.IsNullOrEmpty(Path))
            {
                hash ^= Path.GetHashCode();
            }

            if (Priority.HasValue)
            {
                hash ^= Priority.GetHashCode();
            }

            if (!string.IsNullOrEmpty(ClusterId))
            {
                hash ^= ClusterId.GetHashCode();
            }

            if (!string.IsNullOrEmpty(AuthorizationPolicy))
            {
                hash ^= AuthorizationPolicy.GetHashCode();
            }

            if (!string.IsNullOrEmpty(CorsPolicy))
            {
                hash ^= CorsPolicy.GetHashCode();
            }

            if (Metadata != null)
            {
                hash ^= Metadata.Select(item => HashCode.Combine(item.Key.GetHashCode(), item.Value.GetHashCode()))
                    .Aggregate((total, nextCode) => total ^ nextCode);
            }

            if (Transforms != null)
            {
                hash ^= Transforms.Select(transform =>
                    transform.Select(item => HashCode.Combine(item.Key.GetHashCode(), item.Value.GetHashCode()))
                        .Aggregate((total, nextCode) => total ^ nextCode)) // Unordered Dictionary
                    .Aggregate(seed: 397, (total, nextCode) => total * 31 ^ nextCode); // Ordered List
            }

            return hash;
        }
    }
}
