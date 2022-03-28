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
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing.Tests;

public class QueryParameterMatcherPolicyTests
{
    [Fact]
    public void Comparer_SortOrder_SingleRuleEqual()
    {
        // Most specific to least
        var endpoints = new[]
        {
            (0, CreateEndpoint("queryparam", new[] { "abc" }, QueryParameterMatchMode.Exact, isCaseSensitive: true)),

            (0, CreateEndpoint("queryparam", new[] { "abc" }, QueryParameterMatchMode.Exact)),
            (0, CreateEndpoint("queryparam", new[] { "abc", "def" }, QueryParameterMatchMode.Exact)),
            (0, CreateEndpoint("queryparam2", new[] { "abc", "def" }, QueryParameterMatchMode.Exact)),

            (0, CreateEndpoint("queryparam", new[] { "abc" }, QueryParameterMatchMode.Contains, isCaseSensitive: true)),

            (0, CreateEndpoint("queryparam", new[] { "abc" }, QueryParameterMatchMode.Contains)),
            (0, CreateEndpoint("queryparam", new[] { "abc", "def" }, QueryParameterMatchMode.Contains)),
            (0, CreateEndpoint("queryparam2", new[] { "abc", "def" }, QueryParameterMatchMode.Contains)),

            (0, CreateEndpoint("queryparam", new[] { "abc" }, QueryParameterMatchMode.Prefix)),
            (0, CreateEndpoint("queryparam", new[] { "abc", "def" }, QueryParameterMatchMode.Prefix)),
            (0, CreateEndpoint("queryparam2", new[] { "abc", "def" }, QueryParameterMatchMode.Prefix)),

            (0, CreateEndpoint("queryparam", new string[0], QueryParameterMatchMode.Exists, isCaseSensitive: true)),
            (0, CreateEndpoint("queryparam", new string[0], QueryParameterMatchMode.Exists)),
            (0, CreateEndpoint("queryparam", new string[0], QueryParameterMatchMode.Exists, isCaseSensitive: true)),
            (0, CreateEndpoint("queryparam", new string[0], QueryParameterMatchMode.Exists)),
        };
        var sut = new QueryParameterMatcherPolicy();

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
    public void Comparer_MultipleQueryParameters_SortOrder()
    {
        // Most specific to least
        var endpoints = new[]
        {
            (0, CreateEndpoint(new[]
            {
                new QueryParameterMatcher("queryparam", new string[0], QueryParameterMatchMode.Exists, isCaseSensitive: true),
                new QueryParameterMatcher("queryparam", new[] { "abc" }, QueryParameterMatchMode.Prefix, isCaseSensitive: true),
                new QueryParameterMatcher("queryparam", new[] { "abc" }, QueryParameterMatchMode.Contains, isCaseSensitive: true),
                new QueryParameterMatcher("queryparam", new[] { "abc" }, QueryParameterMatchMode.Exact, isCaseSensitive: true)

            })),

            (1, CreateEndpoint(new[]
            {
                new QueryParameterMatcher("queryparam", new[] { "abc" }, QueryParameterMatchMode.Contains, isCaseSensitive: true),
                new QueryParameterMatcher("queryparam", new[] { "abc" }, QueryParameterMatchMode.Exact, isCaseSensitive: true)
            })),
            (1, CreateEndpoint(new[]
            {
                new QueryParameterMatcher("queryparam", new string[0], QueryParameterMatchMode.Exists, isCaseSensitive: true),
                new QueryParameterMatcher("queryparam", new[] { "abc" }, QueryParameterMatchMode.Exact, isCaseSensitive: true)
            })),

            (2, CreateEndpoint("queryparam", new[] { "abc" })),

            (3, CreateEndpoint(Array.Empty<QueryParameterMatcher>())),

        };
        var sut = new QueryParameterMatcherPolicy();

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
            CreateEndpoint("org-id", new string[0], QueryParameterMatchMode.Exists),
            CreateEndpoint("org-id", new[] { "abc" }),
            CreateEndpoint("org-id", new[] { "abc", "def" }),
            CreateEndpoint("org-id", new string[0], QueryParameterMatchMode.Exists, isDynamic: true),
            CreateEndpoint("org-id", new[] { "abc" }, isDynamic: true),
            CreateEndpoint("org-id", null, QueryParameterMatchMode.Exists, isDynamic: true),
            CreateEndpoint(new[]
            {
                new QueryParameterMatcher("queryParam", new string[0], QueryParameterMatchMode.Exists, isCaseSensitive: true),
                new QueryParameterMatcher("queryParam", new[] { "abc" }, QueryParameterMatchMode.Exact, isCaseSensitive: true)
            })
        };
        var sut = new QueryParameterMatcherPolicy();
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
        var endpoint = CreateEndpoint(Array.Empty<QueryParameterMatcher>());

        var sut = new QueryParameterMatcherPolicy();
        var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

        var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { endpoint });
        Assert.False(result);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abc", true)]
    public async Task ApplyAsync_MatchingScenarios_AnyQueryParamValue(string incomingQueryParamValue, bool shouldMatch)
    {
        var context = new DefaultHttpContext();
        if (incomingQueryParamValue is not null)
        {
            var queryStr = "?org-id=" + incomingQueryParamValue;
            context.Request.QueryString = new QueryString(queryStr);
        }

        var endpoint = CreateEndpoint("org-id", new string[0], QueryParameterMatchMode.Exists);
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new QueryParameterMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Fact]
    public async Task ApplyAsync_MultipleQueryParamValues_NotSupported()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(@"?org-id=a,b");

        var endpoint = CreateEndpoint("org-id", new[] { "a" });
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new QueryParameterMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.False(candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData("abc", QueryParameterMatchMode.Exact, false, null, false)]
    [InlineData("abc", QueryParameterMatchMode.Exact, false, "", false)]
    [InlineData("abc", QueryParameterMatchMode.Exact, false, "abc", true)]
    [InlineData("abc", QueryParameterMatchMode.Exact, false, "aBC", true)]
    [InlineData("abc", QueryParameterMatchMode.Exact, false, "abcd", false)]
    [InlineData("abc", QueryParameterMatchMode.Exact, false, "ab", false)]
    [InlineData("abc", QueryParameterMatchMode.Exact, true, "", false)]
    [InlineData("abc", QueryParameterMatchMode.Exact, true, "abc", true)]
    [InlineData("abc", QueryParameterMatchMode.Exact, true, "aBC", false)]
    [InlineData("abc", QueryParameterMatchMode.Exact, true, "abcd", false)]
    [InlineData("abc", QueryParameterMatchMode.Exact, true, "ab", false)]
    [InlineData("val ue", QueryParameterMatchMode.Contains, false, "val%20ue", true)]
    [InlineData("value", QueryParameterMatchMode.Contains, false, "val%20ue", false)]
    [InlineData("abc", QueryParameterMatchMode.Contains, false, "", false)]
    [InlineData("abc", QueryParameterMatchMode.Contains, false, "aabc", true)]
    [InlineData("abc", QueryParameterMatchMode.Contains, false, "zaBCz", true)]
    [InlineData("abc", QueryParameterMatchMode.Contains, false, "sabcd", true)]
    [InlineData("abc", QueryParameterMatchMode.Contains, false, "aaab", false)]
    [InlineData("abc", QueryParameterMatchMode.Contains, true, "", false)]
    [InlineData("abc", QueryParameterMatchMode.Contains, true, "abcaa", true)]
    [InlineData("abc", QueryParameterMatchMode.Contains, true, "cbcaBC", false)]
    [InlineData("abc", QueryParameterMatchMode.Contains, true, "ababcd", true)]
    [InlineData("abc", QueryParameterMatchMode.Contains, true, "aaaBCd", false)]
    [InlineData("abc", QueryParameterMatchMode.Contains, true, "baba", false)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, false, "", false)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, false, "abc", true)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, false, "aBC", true)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, false, "abcd", true)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, false, "ab", false)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, true, "", false)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, true, "abc", true)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, true, "aBC", false)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, true, "abcd", true)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, true, "aBCd", false)]
    [InlineData("abc", QueryParameterMatchMode.Prefix, true, "ab", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, false, "", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, false, "aabc", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, false, "zaBCz", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, false, "sabcd", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, false, "aaab", true)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, true, "", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, true, "abcaa", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, true, "cbcaBC", true)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, true, "ababcd", false)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, true, "aaaBCd", true)]
    [InlineData("abc", QueryParameterMatchMode.NotContains, true, "baba", true)]
    public async Task ApplyAsync_MatchingScenarios_OneQueryParamValue(
        string queryParamValue,
        QueryParameterMatchMode queryParamValueMatchMode,
        bool isCaseSensitive,
        string incomingQueryParamValue,
        bool shouldMatch)
    {
        var context = new DefaultHttpContext();
        if (incomingQueryParamValue is not null)
        {
            var queryStr = "?org-id=" + incomingQueryParamValue;
            context.Request.QueryString = new QueryString(queryStr);
        }

        var endpoint = CreateEndpoint("org-id", new[] { queryParamValue }, queryParamValueMatchMode, isCaseSensitive);
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new QueryParameterMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, null, false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, "abc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, "aBc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, "abcd", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, "def", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, "deF", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, false, "defg", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, null, false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, "abc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, "aBC", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, "aBCd", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, "def", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, "DEFg", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Exact, true, "dEf", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, null, false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "abc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "aBc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "abcd", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "abcD", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "def", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "deF", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "defg", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "defG", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, false, "abcA", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, null, false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, "abc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, "aBC", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, "aBCd", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, "def", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, "DEFg", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Prefix, true, "abcc", true)]
    [InlineData("val ue", "def", QueryParameterMatchMode.Contains, false, "val%20ue&aabb", true)]
    [InlineData("value", "def", QueryParameterMatchMode.Contains, false, "val%20ue&aabb", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, null, false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "aaaabc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "aBc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "aabcdd", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "ddabcD", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "dedef", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "adeFF", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "degdefg", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "efgdefG", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, false, "AAabc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, null, false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, "abc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, "aBCcba", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, "abaBCd", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, "efdeff", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, "FDEDEFg", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.Contains, true, "aabc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, false, "aaa", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, false, "Abc", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, false, "def", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, false, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, false, null, false)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, true, "aaa", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, true, "Abc", true)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, true, "def", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, true, "", false)]
    [InlineData("abc", "def", QueryParameterMatchMode.NotContains, true, null, false)]
    public async Task ApplyAsync_MatchingScenarios_TwoQueryParamValues(
        string queryParam1Value,
        string queryParam2Value,
        QueryParameterMatchMode queryParamValueMatchMode,
        bool isCaseSensitive,
        string incomingQueryParamValue,
        bool shouldMatch)
    {
        var context = new DefaultHttpContext();
        var queryStr = "?org-id=" + incomingQueryParamValue;
        context.Request.QueryString = new QueryString(queryStr);
        var endpoint = CreateEndpoint("org-id", new[] { queryParam1Value, queryParam2Value }, queryParamValueMatchMode, isCaseSensitive);

        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new QueryParameterMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task ApplyAsync_MultipleRules_RequiresAllQueryParameter(bool sendQueryParam1, bool sendQueryParam2, bool shouldMatch)
    {
        var endpoint = CreateEndpoint(new[]
        {
            new QueryParameterMatcher("queryParam1", new[] { "value1" }, QueryParameterMatchMode.Exact, isCaseSensitive: false),
            new QueryParameterMatcher("queryParam2", new[] { "value2" }, QueryParameterMatchMode.Exact, isCaseSensitive: false)
        });

        var context = new DefaultHttpContext();
        var queryStr = new List<string>();
        if (sendQueryParam1)
        {
            queryStr.Add("queryParam1=value1");
        }
        if (sendQueryParam2)
        {
            queryStr.Add("queryParam2=value2");
        }

        context.Request.QueryString = new QueryString("?" + string.Join("&", queryStr));
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new QueryParameterMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    private static Endpoint CreateEndpoint(
        string queryParamName,
        string[] queryParamValues,
        QueryParameterMatchMode mode = QueryParameterMatchMode.Exact,
        bool isCaseSensitive = false,
        bool isDynamic = false)
    {
        return CreateEndpoint(new[] { new QueryParameterMatcher(queryParamName, queryParamValues, mode, isCaseSensitive) }, isDynamic);
    }

    private static Endpoint CreateEndpoint(IReadOnlyList<QueryParameterMatcher> matchers, bool isDynamic = false)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
        builder.Metadata.Add(new QueryParameterMetadata(matchers));
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
