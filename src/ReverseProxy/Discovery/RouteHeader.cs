// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Discovery
{
    /// <summary>
    /// Route criteria for a header that must be present on the incoming request.
    /// </summary>
    public sealed record RouteHeader
    {
        /// <summary>
        /// Name of the header to look for.
        /// This field is required.
        /// </summary>
        public string Name { get; init; } = default!;

        /// <summary>
        /// A collection of acceptable header values used during routing. Only one value must match.
        /// The list must not be empty unless using <see cref="HeaderMatchMode.Exists"/>.
        /// </summary>
        public IReadOnlyList<string>? Values { get; init; }

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

        /// <inheritdoc />
        public bool Equals(RouteHeader? other)
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

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Mode, IsCaseSensitive,
                IsCaseSensitive
                    ? CaseSensitiveEqualHelper.GetHashCode(Values)
                    : CaseInsensitiveEqualHelper.GetHashCode(Values));
        }
    }
}
