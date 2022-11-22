// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Hosting;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Describes a static response for a matched route
/// </summary>
/// <remarks>
/// <see cref="StatusCode"/>, <see cref="BodyText"/>, or <see cref="BodyFilePath"/> must be set.
/// <see cref="BodyText"/> and <see cref="BodyFilePath"/> are mutually exclusive.
/// </remarks>
public sealed record RouteResponse
{
    /// <summary>
    /// The HTTP status code to use for the response. This must be at least 200.
    /// </summary>
    /// <remarks>
    /// <see cref="StatusCode"/>, <see cref="BodyText"/>, or <see cref="BodyFilePath"/> must be set.
    /// </remarks>
    public int? StatusCode { get; init; }

    /// <summary>
    /// The optional HTTP Content-Type header to use for the response.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The text content to use for the response body.
    /// </summary>
    /// <remarks>
    /// <see cref="StatusCode"/>, <see cref="BodyText"/>, or <see cref="BodyFilePath"/> must be set.
    /// <see cref="BodyText"/> and <see cref="BodyFilePath"/> are mutually exclusive.
    /// </remarks>
    public string? BodyText { get; init; }

    /// <summary>
    /// The path to a file to serve as the response body. This will be retrieved from the
    /// <see cref="IWebHostEnvironment.WebRootFileProvider"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="StatusCode"/>, <see cref="BodyText"/>, or <see cref="BodyFilePath"/> must be set.
    /// <see cref="BodyText"/> and <see cref="BodyFilePath"/> are mutually exclusive.
    /// </remarks>
    public string? BodyFilePath { get; init; }

    /// <inheritdoc/>
    public bool Equals(RouteResponse? other)
    {
        if (other is null)
        {
            return false;
        }

        return StatusCode == other.StatusCode
            && string.Equals(ContentType, other.ContentType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(BodyText, other.BodyText, StringComparison.OrdinalIgnoreCase)
            && string.Equals(BodyFilePath, other.BodyFilePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            StatusCode?.GetHashCode(),
            ContentType?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            BodyText?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            BodyFilePath?.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }
}
