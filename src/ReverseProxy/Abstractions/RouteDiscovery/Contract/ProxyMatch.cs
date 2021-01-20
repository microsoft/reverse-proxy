// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Describes the matching criteria for a route.
    /// </summary>
    public sealed record ProxyMatch : IEquatable<ProxyMatch>
    {
        /// <summary>
        /// Only match requests that use these optional HTTP methods. E.g. GET, POST.
        /// </summary>
        public IReadOnlyList<string> Methods { get; init; }

        /// <summary>
        /// Only match requests with the given Host header.
        /// </summary>
        public IReadOnlyList<string> Hosts { get; init; }

        /// <summary>
        /// Only match requests with the given Path pattern.
        /// </summary>
        public string Path { get; init; }

        // TODO:
        /// <summary>
        /// Only match requests that contain all of these query parameters.
        /// </summary>
        // public ICollection<KeyValuePair<string, string>> QueryParameters { get; init; }

        /// <summary>
        /// Only match requests that contain all of these headers.
        /// </summary>
        public IReadOnlyList<RouteHeader> Headers { get; init; }

        public bool Equals(ProxyMatch other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)
                && CaseInsensitiveEqualHelper.Equals(Hosts, other.Hosts)
                && CaseInsensitiveEqualHelper.Equals(Methods, other.Methods)
                && HeadersEqual(Headers, other.Headers);
        }

        // Order sensitive to reduce complexity
        private static bool HeadersEqual(IReadOnlyList<RouteHeader> headers1, IReadOnlyList<RouteHeader> headers2)
        {
            if (ReferenceEquals(headers1, headers2))
            {
                return true;
            }

            if (headers1 == null || headers2 == null)
            {
                return false;
            }

            if (headers1.Count != headers2.Count)
            {
                return false;
            }

            for (var i = 0; i < headers1.Count; i++)
            {
                if (!headers1[i].Equals(headers2[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
