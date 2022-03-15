// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// A request path parameter matcher used during routing.
/// </summary>
internal sealed class PathParameterMatcher
{
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public PathParameterMatcher(string name, IReadOnlyList<string>? values, PathParameterMatchMode mode, bool isCaseSensitive)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A path parameter name is required.", nameof(name));
        }

        if (values == null || values.Count == 0)
        {
            throw new ArgumentException("Path parameter values must have at least one value.", nameof(values));
        }

        Name = name;
        Values = values;
        Mode = mode;
        IsCaseSensitive = isCaseSensitive;
    }

    /// <summary>
    /// Name of the path parameter to look for.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Returns a read-only collection of acceptable path parameter values used during routing.
    /// </summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>
    /// Specifies how path parameter values should be compared (e.g. exact matches Vs. contains).
    /// Defaults to <see cref="PathParameterMatchMode.Exact"/>.
    /// </summary>
    public PathParameterMatchMode Mode { get; }

    /// <summary>
    /// Specifies whether path parameter value comparisons should ignore case.
    /// When <c>true</c>, <see cref="StringComparison.Ordinal" /> is used.
    /// When <c>false</c>, <see cref="StringComparison.OrdinalIgnoreCase" /> is used.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool IsCaseSensitive { get; }
}
