// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Discovery;

namespace Yarp.ReverseProxy.Routing
{
    /// <summary>
    /// A request header matcher used during routing.
    /// </summary>
    internal sealed class HeaderMatcher
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public HeaderMatcher(string name, IReadOnlyList<string>? values, HeaderMatchMode mode, bool isCaseSensitive)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("A header name is required.", nameof(name));
            }
            if (mode != HeaderMatchMode.Exists
                && (values == null || values.Count == 0))
            {
                throw new ArgumentException("Header values must have at least one value.", nameof(values));
            }
            if (mode == HeaderMatchMode.Exists && values?.Count > 0)
            {
                throw new ArgumentException($"Header values must not be specified when using '{nameof(HeaderMatchMode.Exists)}'.", nameof(values));
            }

            Name = name;
            Values = values ?? Array.Empty<string>();
            Mode = mode;
            IsCaseSensitive = isCaseSensitive;
        }

        /// <summary>
        /// Name of the header to look for.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns a read-only collection of acceptable header values used during routing.
        /// At least one value is required unless <see cref="Mode"/> is set to <see cref="HeaderMatchMode.Exists"/>.
        /// </summary>
        public IReadOnlyList<string> Values { get; }

        /// <summary>
        /// Specifies how header values should be compared (e.g. exact matches Vs. by prefix).
        /// Defaults to <see cref="HeaderMatchMode.ExactHeader"/>.
        /// </summary>
        public HeaderMatchMode Mode { get; }

        /// <summary>
        /// Specifies whether header value comparisons should ignore case.
        /// When <c>true</c>, <see cref="StringComparison.Ordinal" /> is used.
        /// When <c>false</c>, <see cref="StringComparison.OrdinalIgnoreCase" /> is used.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool IsCaseSensitive { get; }
    }
}
