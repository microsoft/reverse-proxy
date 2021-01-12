// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Describes the matching criteria for a route.
    /// </summary>
    public class ProxyMatch : IDeepCloneable<ProxyMatch>
    {
        /// <summary>
        /// Only match requests that use these optional HTTP methods. E.g. GET, POST.
        /// </summary>
        public IReadOnlyList<string> Methods { get; set; }

        /// <summary>
        /// Only match requests with the given Host header.
        /// </summary>
        public IReadOnlyList<string> Hosts { get; set; }

        /// <summary>
        /// Only match requests with the given Path pattern.
        /// </summary>
        public string Path { get; set; }

        // TODO:
        /// <summary>
        /// Only match requests that contain all of these query parameters.
        /// </summary>
        // public ICollection<KeyValuePair<string, string>> QueryParameters { get; set; }

        /// <summary>
        /// Only match requests that contain all of these headers.
        /// </summary>
        public IReadOnlyList<RouteHeader> Headers { get; set; }

        ProxyMatch IDeepCloneable<ProxyMatch>.DeepClone()
        {
            return new ProxyMatch()
            {
                Methods = Methods?.ToArray(),
                Hosts = Hosts?.ToArray(),
                Path = Path,
                Headers = Headers?.ToArray(),
            };
        }

        internal static bool Equals(ProxyMatch proxyMatch1, ProxyMatch proxyMatch2)
        {
            if (proxyMatch1 == null && proxyMatch2 == null)
            {
                return true;
            }

            if (proxyMatch1 == null || proxyMatch2 == null)
            {
                return false;
            }

            return string.Equals(proxyMatch1.Path, proxyMatch2.Path, StringComparison.OrdinalIgnoreCase)
                && CaseInsensitiveEqualHelper.Equals(proxyMatch1.Hosts, proxyMatch2.Hosts)
                && CaseInsensitiveEqualHelper.Equals(proxyMatch1.Methods, proxyMatch2.Methods)
                && HeadersEqual(proxyMatch1.Headers, proxyMatch2.Headers);
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
                if (!RouteHeader.Equals(headers1[i], headers2[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
