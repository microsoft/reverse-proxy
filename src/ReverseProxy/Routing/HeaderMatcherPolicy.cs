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
using Yarp.ReverseProxy.Discovery;

namespace Yarp.ReverseProxy.Service.Routing
{
    internal sealed class HeaderMatcherPolicy : MatcherPolicy, IEndpointComparerPolicy, IEndpointSelectorPolicy
    {
        /// <inheritdoc/>
        // Run after HttpMethodMatcherPolicy (-1000) and HostMatcherPolicy (-100), but before default (0)
        public override int Order => -50;

        /// <inheritdoc/>
        public IComparer<Endpoint> Comparer => new HeaderMetadataEndpointComparer();

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
                var metadata = e.Metadata.GetMetadata<IHeaderMetadata>();
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

                var matchers = candidates[i].Endpoint.Metadata.GetMetadata<IHeaderMetadata>()?.Matchers;

                if (matchers == null)
                {
                    continue;
                }

                for (var m = 0; m < matchers.Count; m++)
                {
                    var matcher = matchers[m];
                    var expectedHeaderName = matcher.Name;
                    var expectedHeaderValues = matcher.Values;

                    var matched = false;
                    if (httpContext.Request.Headers.TryGetValue(expectedHeaderName, out var requestHeaderValues))
                    {
                        if (StringValues.IsNullOrEmpty(requestHeaderValues))
                        {
                            // A non-empty value is required for a match.
                        }
                        else if (matcher.Mode == HeaderMatchMode.Exists)
                        {
                            // We were asked to match as long as the header exists, and it *does* exist
                            matched = true;
                        }
                        // Multi-value headers are not supported.
                        // Note a single entry may also contain multiple values, we don't distinguish, we only match on the whole header.
                        else if (requestHeaderValues.Count == 1)
                        {
                            var requestHeaderValue = requestHeaderValues.ToString();
                            for (var j = 0; j < expectedHeaderValues.Count; j++)
                            {
                                if (MatchHeader(matcher.Mode, requestHeaderValue, expectedHeaderValues[j], matcher.IsCaseSensitive))
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

        private static bool MatchHeader(HeaderMatchMode matchMode, string requestHeaderValue, string metadataHeaderValue, bool isCaseSensitive)
        {
            var comparison = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return matchMode switch
            {
                HeaderMatchMode.ExactHeader => MemoryExtensions.Equals(requestHeaderValue, metadataHeaderValue, comparison),
                HeaderMatchMode.HeaderPrefix => requestHeaderValue != null && metadataHeaderValue != null
                    && MemoryExtensions.StartsWith(requestHeaderValue, metadataHeaderValue, comparison),
                _ => throw new NotImplementedException(matchMode.ToString()),
            };
        }

        private class HeaderMetadataEndpointComparer : EndpointMetadataComparer<IHeaderMetadata>
        {
            protected override int CompareMetadata(IHeaderMetadata? x, IHeaderMetadata? y)
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
}
