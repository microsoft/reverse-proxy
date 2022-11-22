// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Describes a static file response for a matched route
/// </summary>
public sealed record RouteFiles
{
    /// <summary>
    /// Serve static files from this location. This is relative to the application content root.
    /// Route template parameters may be used.
    /// </summary>
    public string? Root { get; init; }

    public bool Equals(RouteFiles? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Root, other.Root, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Root?.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }
}
