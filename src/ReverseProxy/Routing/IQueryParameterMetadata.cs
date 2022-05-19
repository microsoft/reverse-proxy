// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// Represents request query parameter metadata used during routing.
/// </summary>
internal interface IQueryParameterMetadata
{
    /// <summary>
    /// One or more matchers to apply to the request query parameters.
    /// </summary>
    QueryParameterMatcher[] Matchers { get; }
}
