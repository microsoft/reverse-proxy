// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing;

internal sealed class QueryParameterMatcherPolicy : MatcherPolicy, IEndpointComparerPolicy, IEndpointSelectorPolicy
{
    /// <inheritdoc/>
    // Run after HttpMethodMatcherPolicy (-1000) and HostMatcherPolicy (-100), and HeaderMatcherPolicy (-50), but before default (0)
    public override int Order => -25;

    /// <inheritdoc/>
    public IComparer<Endpoint> Comparer => new QueryParameterMetadataEndpointComparer();

    /// <inheritdoc/>
    bool IEndpointSelectorPolicy.AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        _ = endpoints ?? throw new ArgumentNullException(nameof(endpoints));

        // When the node contains dynamic endpoints we can't make any assumptions.
        if (ContainsDynamicEndpoints(endpoints))
        {
            return true;
        }

        return AppliesToEndpointsCore(endpoints);
    }

    private static bool AppliesToEndpointsCore(IReadOnlyList<Endpoint> endpoints)
    {
        return endpoints.Any(e =>
        {
            var metadata = e.Metadata.GetMetadata<IQueryParameterMetadata>();
            return metadata?.Matchers?.Count > 0;
        });
    }

    /// <inheritdoc/>
    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        _ = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        _ = candidates ?? throw new ArgumentNullException(nameof(candidates));

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            var matchers = candidates[i].Endpoint.Metadata.GetMetadata<IQueryParameterMetadata>()?.Matchers;

            if (matchers is null)
            {
                continue;
            }

            for (var m = 0; m < matchers.Count; m++)
            {
                var matcher = matchers[m];
                var expectedQueryParameterName = matcher.Name;
                var expectedQueryParameterValues = matcher.Values;

                var matched = false;
                if (httpContext.Request.Query.TryGetValue(expectedQueryParameterName, out var requestQueryParameterValues))
                {
                    if (StringValues.IsNullOrEmpty(requestQueryParameterValues))
                    {
                        // A non-empty value is required for a match.
                    }
                    else if (matcher.Mode == QueryParameterMatchMode.Exists)
                    {
                        // We were asked to match as long as the query parameter exists, and it *does* exist
                        matched = true;
                    }
                    // Multi-value query parameters are not supported.
                    else if (requestQueryParameterValues.Count == 1)
                    {
                        var requestQueryParameterValue = requestQueryParameterValues.ToString();
                        for (var j = 0; j < expectedQueryParameterValues.Count; j++)
                        {
                            if (MatchQueryParameter(matcher.Mode, requestQueryParameterValue, expectedQueryParameterValues[j], matcher.IsCaseSensitive))
                            {
                                if (matcher.Mode == QueryParameterMatchMode.NotContains)
                                {
                                    if (j + 1 == expectedQueryParameterValues.Count)
                                    {
                                        // None of the NotContains values were found
                                        matched = true;
                                    }
                                }
                                else
                                {
                                    matched = true;
                                    break;
                                }
                            }
                            else if (matcher.Mode == QueryParameterMatchMode.NotContains)
                            {
                                break;
                            }
                        }

                    }
                }

                // All rules must match
                if (!matched)
                {
                    candidates.SetValidity(i, false);
                    break;
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool MatchQueryParameter(QueryParameterMatchMode matchMode, string requestQueryParameterValue, string metadataQueryParameterValue, bool isCaseSensitive)
    {
        var comparison = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return matchMode switch
        {
            QueryParameterMatchMode.Exact => MemoryExtensions.Equals(requestQueryParameterValue, metadataQueryParameterValue, comparison),
            QueryParameterMatchMode.Prefix => requestQueryParameterValue is not null && metadataQueryParameterValue is not null
                && MemoryExtensions.StartsWith(requestQueryParameterValue, metadataQueryParameterValue, comparison),
            QueryParameterMatchMode.Contains => requestQueryParameterValue is not null && metadataQueryParameterValue is not null
                && MemoryExtensions.Contains(requestQueryParameterValue, metadataQueryParameterValue, comparison),
            QueryParameterMatchMode.NotContains => requestQueryParameterValue is not null && metadataQueryParameterValue is not null
                && !MemoryExtensions.Contains(requestQueryParameterValue, metadataQueryParameterValue, comparison),
            _ => throw new NotImplementedException(matchMode.ToString()),
        };
    }

    private class QueryParameterMetadataEndpointComparer : EndpointMetadataComparer<IQueryParameterMetadata>
    {
        protected override int CompareMetadata(IQueryParameterMetadata? x, IQueryParameterMetadata? y)
        {
            var xCount = x?.Matchers?.Count ?? 0;
            var yCount = y?.Matchers?.Count ?? 0;

            if (xCount > yCount)
            {
                // x is more specific
                return -1;
            }
            if (yCount > xCount)
            {
                // y is more specific
                return 1;
            }

            return 0;
        }
    }
}
