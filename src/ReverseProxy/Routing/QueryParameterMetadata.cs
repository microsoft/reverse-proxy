// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// Represents request query parameter metadata used during routing.
/// </summary>
internal sealed class QueryParameterMetadata : IQueryParameterMetadata
{
    public QueryParameterMetadata(IReadOnlyList<QueryParameterMatcher> matchers)
    {
        Matchers = matchers?.ToArray() ?? throw new ArgumentNullException(nameof(matchers));
    }

    /// <inheritdoc/>
    public QueryParameterMatcher[] Matchers { get; }
}
