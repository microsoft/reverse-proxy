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

        // TODO:
        /// <summary>
        /// Only match requests that contain all of these request headers.
        /// </summary>
        // public ICollection<KeyValuePair<string, string>> Headers { get; set; }

        ProxyMatch IDeepCloneable<ProxyMatch>.DeepClone()
        {
            return new ProxyMatch()
            {
                Methods = Methods?.ToArray(),
                Hosts = Hosts?.ToArray(),
                Path = Path,
                // Headers = Headers.DeepClone(); // TODO:
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
                && CaseInsensitiveEqualHelper.Equals(proxyMatch1.Methods, proxyMatch2.Methods);
        }
    }
}
