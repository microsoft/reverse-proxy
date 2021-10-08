// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing
{
    /// <summary>
    /// A request query parameter matcher used during routing.
    /// </summary>
    internal sealed class QueryParameterMatcher
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public QueryParameterMatcher(string name, IReadOnlyList<string>? values, QueryParameterMatchMode mode, bool isCaseSensitive)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("A query parameter name is required.", nameof(name));
            }
            if (mode != QueryParameterMatchMode.Exists
                && (values == null || values.Count == 0))
            {
                throw new ArgumentException("Query parameter values must have at least one value.", nameof(values));
            }
            if (mode == QueryParameterMatchMode.Exists && values?.Count > 0)
            {
                throw new ArgumentException($"Query parameter values must not be specified when using '{nameof(QueryParameterMatchMode.Exists)}'.", nameof(values));
            }

            Name = name;
            Values = values ?? Array.Empty<string>();
            Mode = mode;
            IsCaseSensitive = isCaseSensitive;
        }

        /// <summary>
        /// Name of the query parameter to look for.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns a read-only collection of acceptable query parameter values used during routing.
        /// At least one value is required unless <see cref="Mode"/> is set to <see cref="QueryParameterMatchMode.Exists"/>.
        /// </summary>
        public IReadOnlyList<string> Values { get; }

        /// <summary>
        /// Specifies how query parameter values should be compared (e.g. exact matches Vs. contains).
        /// Defaults to <see cref="QueryParameterMatchMode.Exact"/>.
        /// </summary>
        public QueryParameterMatchMode Mode { get; }

        /// <summary>
        /// Specifies whether query parameter value comparisons should ignore case.
        /// When <c>true</c>, <see cref="StringComparison.Ordinal" /> is used.
        /// When <c>false</c>, <see cref="StringComparison.OrdinalIgnoreCase" /> is used.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool IsCaseSensitive { get; }
    }
}
