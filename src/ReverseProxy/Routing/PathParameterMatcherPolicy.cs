// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing;

internal sealed class PathParameterMatcherPolicy : MatcherPolicy, IEndpointComparerPolicy, IEndpointSelectorPolicy
{
    /// <inheritdoc/>
    // Run after HttpMethodMatcherPolicy (-1000) and HostMatcherPolicy (-100), but before HeaderMatcherPolicy (-50)
    public override int Order => -75;

    /// <inheritdoc/>
    public IComparer<Endpoint> Comparer => new PathParameterMetadataEndpointComparer();

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
            var metadata = e.Metadata.GetMetadata<IPathParameterMetadata>();
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

            var candidate = candidates[i];

            var matchers = candidate.Endpoint.Metadata.GetMetadata<IPathParameterMetadata>()?.Matchers;

            if (matchers == null)
            {
                continue;
            }

            for (var m = 0; m < matchers.Count; m++)
            {
                var matcher = matchers[m];
                var expectedPathParameterName = matcher.Name;
                var expectedPathParameterValues = matcher.Values;

                var matched = false;

                if (candidate.Values is not null && candidate.Values.TryGetValue(expectedPathParameterName, out var requestPathParameterObject))
                {
                    if (requestPathParameterObject is not null && requestPathParameterObject is string)
                    {
                        var requestPathParameterValue = (string)requestPathParameterObject;

                        for (var j = 0; j < expectedPathParameterValues.Count; j++)
                        {
                            if (MatchPathParameter(matcher.Mode, requestPathParameterValue, expectedPathParameterValues[j], matcher.IsCaseSensitive))
                            {
                                matched = true;
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

    private static bool MatchPathParameter(PathParameterMatchMode matchMode, string requestPathParameterValue, string metadataPathParameterValue, bool isCaseSensitive)
    {
        var comparison = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return matchMode switch
        {
            PathParameterMatchMode.Prefix => requestPathParameterValue != null && metadataPathParameterValue != null
                && MemoryExtensions.StartsWith(requestPathParameterValue, metadataPathParameterValue, comparison),
            PathParameterMatchMode.NotPrefix => requestPathParameterValue != null && metadataPathParameterValue != null
                && !MemoryExtensions.StartsWith(requestPathParameterValue, metadataPathParameterValue, comparison),
            _ => throw new NotImplementedException(matchMode.ToString()),
        };
    }

    private class PathParameterMetadataEndpointComparer : EndpointMetadataComparer<IPathParameterMetadata>
    {
        protected override int CompareMetadata(IPathParameterMetadata? x, IPathParameterMetadata? y)
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
