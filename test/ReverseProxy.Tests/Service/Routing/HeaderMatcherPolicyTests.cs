// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.Service.Routing
{
    public class HeaderMatcherPolicyTests
    {
        [Fact]
        public void Comparer_SortOrder_SingleRuleEqual()
        {
            // Most specific to least
            var endpoints = new[]
            {
                (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)),

                (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.ExactHeader)),
                (0, CreateEndpoint("header", new[] { "abc", "def" }, HeaderMatchMode.ExactHeader)),
                (0, CreateEndpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.ExactHeader)),

                (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix, isCaseSensitive: true)),

                (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix)),
                (0, CreateEndpoint("header", new[] { "abc", "def" }, HeaderMatchMode.HeaderPrefix)),
                (0, CreateEndpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.HeaderPrefix)),

                (0, CreateEndpoint("header", new string[0], HeaderMatchMode.Exists, isCaseSensitive: true)),
                (0, CreateEndpoint("header", new string[0], HeaderMatchMode.Exists)),
                (0, CreateEndpoint("header", new string[0], HeaderMatchMode.Exists, isCaseSensitive: true)),
                (0, CreateEndpoint("header", new string[0], HeaderMatchMode.Exists)),
            };
            var sut = new HeaderMatcherPolicy();

            for (var i = 0; i < endpoints.Length; i++)
            {
                for (var j = 0; j < endpoints.Length; j++)
                {
                    var a = endpoints[i];
                    var b = endpoints[j];

                    var actual = sut.Comparer.Compare(a.Item2, b.Item2);
                    var expected =
                        a.Item1 < b.Item1 ? -1 :
                        a.Item1 > b.Item1 ? 1 : 0;
                    if (actual != expected)
                    {
                        Assert.True(false, $"Error comparing [{i}] to [{j}], expected {expected}, found {actual}.");
                    }
                }
            }
        }

        [Fact]
        public void Comparer_MultipleHeaders_SortOrder()
        {
            // Most specific to least
            var endpoints = new[]
            {
                (0, CreateEndpoint(new[]
                {
                    new HeaderMatcher("header", new string[0], HeaderMatchMode.Exists, isCaseSensitive: true),
                    new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix, isCaseSensitive: true),
                    new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
                })),

                (1, CreateEndpoint(new[]
                {
                    new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix, isCaseSensitive: true),
                    new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
                })),
                (1, CreateEndpoint(new[]
                {
                    new HeaderMatcher("header", new string[0], HeaderMatchMode.Exists, isCaseSensitive: true),
                    new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
                })),

                (2, CreateEndpoint("header", new[] { "abc" })),

                (3, CreateEndpoint(Array.Empty<HeaderMatcher>())),

            };
            var sut = new HeaderMatcherPolicy();

            for (var i = 0; i < endpoints.Length; i++)
            {
                for (var j = 0; j < endpoints.Length; j++)
                {
                    var a = endpoints[i];
                    var b = endpoints[j];

                    var actual = sut.Comparer.Compare(a.Item2, b.Item2);
                    var expected =
                        a.Item1 < b.Item1 ? -1 :
                        a.Item1 > b.Item1 ? 1 : 0;
                    if (actual != expected)
                    {
                        Assert.True(false, $"Error comparing [{i}] to [{j}], expected {expected}, found {actual}.");
                    }
                }
            }
        }

        [Fact]
        public void AppliesToEndpoints_AppliesScenarios()
        {
            var scenarios = new[]
            {
                CreateEndpoint("org-id", new string[0], HeaderMatchMode.Exists),
                CreateEndpoint("org-id", new[] { "abc" }),
                CreateEndpoint("org-id", new[] { "abc", "def" }),
                CreateEndpoint("org-id", new string[0], HeaderMatchMode.Exists, isDynamic: true),
                CreateEndpoint("org-id", new[] { "abc" }, isDynamic: true),
                CreateEndpoint("org-id", null, HeaderMatchMode.Exists, isDynamic: true),
                CreateEndpoint(new[]
                {
                    new HeaderMatcher("header", new string[0], HeaderMatchMode.Exists, isCaseSensitive: true),
                    new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
                })
            };
            var sut = new HeaderMatcherPolicy();
            var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

            for (var i = 0; i < scenarios.Length; i++)
            {
                var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { scenarios[i] });
                Assert.True(result, $"scenario {i}");
            }
        }

        [Fact]
        public void AppliesToEndpoints_NoMetadata_DoesNotApply()
        {
            var endpoint = CreateEndpoint(Array.Empty<HeaderMatcher>());

            var sut = new HeaderMatcherPolicy();
            var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

            var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { endpoint });
            Assert.False(result);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("abc", true)]
        public async Task ApplyAsync_MatchingScenarios_AnyHeaderValue(string incomingHeaderValue, bool shouldMatch)
        {
            var context = new DefaultHttpContext();
            if (incomingHeaderValue != null)
            {
                context.Request.Headers.Add("org-id", incomingHeaderValue);
            }

            var endpoint = CreateEndpoint("org-id", new string[0], HeaderMatchMode.Exists);
            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            await sut.ApplyAsync(context, candidates);

            Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
        }

        [Fact]
        public async Task ApplyAsync_MultipleHeaderValues_NotSupported()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Add("org-id", new[] { "a", "b" });

            var endpoint = CreateEndpoint("org-id", new[] { "a" });
            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            await sut.ApplyAsync(context, candidates);

            Assert.False(candidates.IsValidCandidate(0));
        }

        [Theory]
        [InlineData("abc", HeaderMatchMode.ExactHeader, false, null, false)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, false, "", false)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abc", true)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, false, "aBC", true)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abcd", false)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, false, "ab", false)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, true, "", false)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, true, "abc", true)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, true, "aBC", false)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, true, "abcd", false)]
        [InlineData("abc", HeaderMatchMode.ExactHeader, true, "ab", false)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "", false)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "abc", true)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "aBC", true)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "abcd", true)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "ab", false)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "", false)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "abc", true)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "aBC", false)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "abcd", true)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "aBCd", false)]
        [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "ab", false)]
        public async Task ApplyAsync_MatchingScenarios_OneHeaderValue(
            string headerValue,
            HeaderMatchMode headerValueMatchMode,
            bool isCaseSensitive,
            string incomingHeaderValue,
            bool shouldMatch)
        {
            var context = new DefaultHttpContext();
            if (incomingHeaderValue != null)
            {
                context.Request.Headers.Add("org-id", incomingHeaderValue);
            }

            var endpoint = CreateEndpoint("org-id", new[] { headerValue }, headerValueMatchMode, isCaseSensitive);
            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            await sut.ApplyAsync(context, candidates);

            Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
        }

        [Theory]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "aBc", true)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "abcd", false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "deF", true)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "defg", false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "aBC", false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "aBCd", false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "DEFg", false)]
        [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "dEf", false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "aBc", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "abcd", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "abcD", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "deF", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "defg", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "defG", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "aabc", false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "aBC", false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "aBCd", false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "DEFg", false)]
        [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "aabc", false)]
        public async Task ApplyAsync_MatchingScenarios_TwoHeaderValues(
            string header1Value,
            string header2Value,
            HeaderMatchMode headerValueMatchMode,
            bool isCaseSensitive,
            string incomingHeaderValue,
            bool shouldMatch)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Add("org-id", incomingHeaderValue);
            var endpoint = CreateEndpoint("org-id", new[] { header1Value, header2Value }, headerValueMatchMode, isCaseSensitive);

            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            await sut.ApplyAsync(context, candidates);

            Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public async Task ApplyAsync_MultipleRules_RequiresAllHeaders(bool sendHeader1, bool sendHeader2, bool shouldMatch)
        {
            var endpoint = CreateEndpoint(new[]
            {
                new HeaderMatcher("header1", new[] { "value1" }, HeaderMatchMode.ExactHeader, isCaseSensitive: false),
                new HeaderMatcher("header2", new[] { "value2" }, HeaderMatchMode.ExactHeader, isCaseSensitive: false)
            });

            var context = new DefaultHttpContext();
            if (sendHeader1)
            {
                context.Request.Headers.Add("header1", "value1");
            }
            if (sendHeader2)
            {
                context.Request.Headers.Add("header2", "value2");
            }

            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            await sut.ApplyAsync(context, candidates);

            Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
        }

        private static Endpoint CreateEndpoint(
            string headerName,
            string[] headerValues,
            HeaderMatchMode mode = HeaderMatchMode.ExactHeader,
            bool isCaseSensitive = false,
            bool isDynamic = false)
        {
            return CreateEndpoint(new[] { new HeaderMatcher(headerName, headerValues, mode, isCaseSensitive) }, isDynamic);
        }

        private static Endpoint CreateEndpoint(IReadOnlyList<HeaderMatcher> matchers, bool isDynamic = false)
        {
            var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
            builder.Metadata.Add(new HeaderMetadata(matchers));
            if (isDynamic)
            {
                builder.Metadata.Add(new DynamicEndpointMetadata());
            }

            return builder.Build();
        }

        private class DynamicEndpointMetadata : IDynamicEndpointMetadata
        {
            public bool IsDynamic => true;
        }
    }
}
