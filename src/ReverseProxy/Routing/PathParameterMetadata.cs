// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// Represents request path parameter metadata used during routing.
/// </summary>
internal sealed class PathParameterMetadata : IPathParameterMetadata
{
    public PathParameterMetadata(IReadOnlyList<PathParameterMatcher> matchers)
    {
        Matchers = matchers ?? throw new ArgumentNullException(nameof(matchers));
    }

    /// <inheritdoc/>
    public IReadOnlyList<PathParameterMatcher> Matchers { get; }
}
