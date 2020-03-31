// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;
using Microsoft.ReverseProxy.Core.Service;

namespace Microsoft.ReverseProxy.Core.ConfigModel
{
    // TODO: Do we even need the ParsedRoute? It now matches the GatewayRoute 1:1
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
        /// Gets or sets the backend that requests matching this route
        /// should be proxied to.
        /// </summary>
        public string BackendId { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this route.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        internal string GetMatcherSummary()
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(Host))
            {
                builder.AppendFormat("Host({0});", Host);
            }

            if (!string.IsNullOrEmpty(Path))
            {
                builder.AppendFormat("Path({0});", Path);
            }

            if (Methods != null && Methods.Count > 0)
            {
                builder.Append("Methods(");
                builder.AppendJoin(',', Methods);
                builder.Append(");");
            }

            return builder.ToString();
        }
    }
}
