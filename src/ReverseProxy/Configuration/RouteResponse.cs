// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Describes a static response for a matched route
/// </summary>
public sealed record RouteResponse
{
    public int? StatusCode { get; set; }

    public string? ContentType { get; set; }

    public string? Body { get; set; }

    public string? File { get; set; }

    public bool Equals(RouteResponse? other)
    {
        if (other is null)
        {
            return false;
        }

        return StatusCode == other.StatusCode
            && string.Equals(ContentType, other.ContentType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Body, other.Body, StringComparison.OrdinalIgnoreCase)
            && string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            StatusCode?.GetHashCode(),
            ContentType?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            Body?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            File?.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }
}
