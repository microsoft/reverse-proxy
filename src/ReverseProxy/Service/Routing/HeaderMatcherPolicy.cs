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
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service.Routing
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

                var metadataList = candidates[i].Endpoint.Metadata.GetOrderedMetadata<IHeaderMetadata>();

                for (var m = 0; m < metadataList.Count; m++)
                {
                    var metadata = metadataList[m];
                    var metadataHeaderName = metadata.HeaderName;
                    var metadataHeaderValues = metadata.HeaderValues;

                    // Also checked in the HeaderMetadata constructor.
                    if (string.IsNullOrEmpty(metadataHeaderName))
                    {
                        throw new InvalidOperationException("A header name must be specified.");
                    }
                    if (metadata.Mode != HeaderMatchMode.Exists
                        && (metadataHeaderValues == null || metadataHeaderValues.Count == 0))
                    {
                        throw new InvalidOperationException("IHeaderMetadata.HeaderValues must have at least one value.");
                    }

                    var matched = false;
                    if (httpContext.Request.Headers.TryGetValue(metadataHeaderName, out var requestHeaderValues))
                    {
                        if (StringValues.IsNullOrEmpty(requestHeaderValues))
                        {
                            // A non-empty value is required for a match.
                        }
                        else if (metadata.Mode == HeaderMatchMode.Exists)
                        {
                            // We were asked to match as long as the header exists, and it *does* exist
                            matched = true;
                        }
                        // Multi-value headers are not supported.
                        // Note a single entry may also contain multiple values, we don't distinguish, we only match on the whole header.
                        else if (requestHeaderValues.Count == 1)
                        {
                            var requestHeaderValue = requestHeaderValues.ToString();
                            for (var j = 0; j < metadataHeaderValues.Count; j++)
                            {
                                if (MatchHeader(metadata.Mode, requestHeaderValue, metadataHeaderValues[j], metadata.CaseSensitive))
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

        private static bool MatchHeader(HeaderMatchMode matchMode, string requestHeaderValue, string metadataHeaderValue, bool caseSensitive)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return matchMode switch
            {
                HeaderMatchMode.ExactHeader => MemoryExtensions.Equals(requestHeaderValue, metadataHeaderValue, comparison),
                HeaderMatchMode.HeaderPrefix => requestHeaderValue != null && metadataHeaderValue != null
                    && MemoryExtensions.StartsWith(requestHeaderValue, metadataHeaderValue, comparison),
                _ => throw new NotImplementedException(matchMode.ToString()),
            };
        }

        private static bool AppliesToEndpointsCore(IReadOnlyList<Endpoint> endpoints)
        {
            return endpoints.Any(e =>
            {
                var metadata = e.Metadata.GetMetadata<IHeaderMetadata>();
                return metadata != null;
            });
        }

        private class HeaderMetadataEndpointComparer : EndpointMetadataComparer<IHeaderMetadata>
        {
            protected override int CompareMetadata(IHeaderMetadata x, IHeaderMetadata y)
            {
                var xPresent = !string.IsNullOrEmpty(x?.HeaderName);
                var yPresent = !string.IsNullOrEmpty(y?.HeaderName);

                // 1. First, sort by presence of metadata
                if (!xPresent && yPresent)
                {
                    // y is more specific
                    return 1;
                }
                else if (xPresent && !yPresent)
                {
                    // x is more specific
                    return -1;
                }
                else if (!xPresent && !yPresent)
                {
                    // None of the policies have any effect, so they have same specificity.
                    return 0;
                }

                // 2. Then, by whether we seek specific header values or just header presence
                var xCount = x.HeaderValues?.Count ?? 0;
                var yCount = y.HeaderValues?.Count ?? 0;

                if (xCount == 0 && yCount > 0)
                {
                    // y is more specific, as *only it* looks for specific header values
                    return 1;
                }
                else if (xCount > 0 && yCount == 0)
                {
                    // x is more specific, as *only it* looks for specific header values
                    return -1;
                }
                else if (xCount == 0 && yCount == 0)
                {
                    // Same specificity, they both only check eader presence
                    return 0;
                }

                // 3. Then, by value match mode (Exact Vs. Prefix)
                if (x.Mode != HeaderMatchMode.ExactHeader && y.Mode == HeaderMatchMode.ExactHeader)
                {
                    // y is more specific, as *only it* does exact match
                    return 1;
                }
                else if (x.Mode == HeaderMatchMode.ExactHeader && y.Mode != HeaderMatchMode.ExactHeader)
                {
                    // x is more specific, as *only it* does exact match
                    return -1;
                }

                // 4. Then, by case sensitivity
                if (x.CaseSensitive && !y.CaseSensitive)
                {
                    // x is more specific, as *only it* is case sensitive
                    return -1;
                }
                else if (!x.CaseSensitive && y.CaseSensitive)
                {
                    // y is more specific, as *only it* is case sensitive
                    return 1;
                }

                // They have equal specificity
                return 0;
            }
        }
    }
}
