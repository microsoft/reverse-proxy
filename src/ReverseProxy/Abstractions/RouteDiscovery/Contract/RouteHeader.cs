// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Route criteria for a header that must be present on the incoming request.
    /// </summary>
    public sealed record RouteHeader : IEquatable<RouteHeader>
    {
        /// <summary>
        /// Name of the header to look for.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// A collection of acceptable header values used during routing. Only one value must match.
        /// The list must not be empty unless using <see cref="HeaderMatchMode.Exists"/>.
        /// </summary>
        public IReadOnlyList<string> Values { get; init; }

        /// <summary>
        /// Specifies how header values should be compared (e.g. exact matches Vs. by prefix).
        /// Defaults to <see cref="HeaderMatchMode.ExactHeader"/>.
        /// </summary>
        public HeaderMatchMode Mode { get; init; }

        /// <summary>
        /// Specifies whether header value comparisons should ignore case.
        /// When <c>true</c>, <see cref="StringComparison.Ordinal" /> is used.
        /// When <c>false</c>, <see cref="StringComparison.OrdinalIgnoreCase" /> is used.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool IsCaseSensitive { get; init; }

        /// <summary>
        /// Checks if this instance is equal to the given one.
        /// </summary>
        public bool Equals(RouteHeader other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                && Mode == other.Mode
                && IsCaseSensitive == other.IsCaseSensitive
                && (IsCaseSensitive
                    ? CaseSensitiveEqualHelper.Equals(Values, other.Values)
                    : CaseInsensitiveEqualHelper.Equals(Values, other.Values));
        }
    }
}
