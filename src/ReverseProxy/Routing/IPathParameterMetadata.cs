// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// Represents request path parameter metadata used during routing.
/// </summary>
internal interface IPathParameterMetadata
{
    /// <summary>
    /// One or more matchers to apply to the request path parameters.
    /// </summary>
    IReadOnlyList<PathParameterMatcher> Matchers { get; }
}
