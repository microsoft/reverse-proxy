// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.Routing
{
    /// <summary>
    /// Represents request header metadata used during routing.
    /// </summary>
    internal class HeaderMetadata : IHeaderMetadata
    {
        public HeaderMetadata(string headerName, IReadOnlyList<string> headerValues, HeaderMatchMode mode, bool caseSensitive)
        {
            HeaderName = headerName;
            HeaderValues = headerValues;
            Mode = mode;
            CaseSensitive = caseSensitive;
        }

        /// <summary>
        /// Name of the header to look for.
        /// </summary>
        public string HeaderName { get; }

        /// <summary>
        /// Returns a read-only collection of acceptable header values used during routing.
        /// An empty collection means any header value will be accepted, as long as the header is present.
        /// </summary>
        public IReadOnlyList<string> HeaderValues { get; }

        /// <summary>
        /// Specifies how header values should be compared (e.g. exact matches Vs. by prefix).
        /// Defaults to <see cref="HeaderMatchMode.Exact"/>.
        /// </summary>
        public HeaderMatchMode Mode { get; }

        /// <summary>
        /// Specifies whether header value comparisons should ignore case.
        /// When <c>true</c>, <see cref="StringComparison.Ordinal" /> is used.
        /// When <c>false</c>, <see cref="StringComparison.OrdinalIgnoreCase" /> is used.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool CaseSensitive { get; }
    }
}
