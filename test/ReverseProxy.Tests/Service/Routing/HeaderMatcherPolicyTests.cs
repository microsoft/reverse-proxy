// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Routing
{
    public class HeaderMatcherPolicyTests
    {
        [Fact]
        public void Comparer_SortOrder()
        {
            // Arrange
            var endpoints = new[]
            {
                (0, Endpoint("header", new[] { "abc" }, HeaderValueMatchMode.Exact, caseSensitive: true)),

                (1, Endpoint("header", new[] { "abc" }, HeaderValueMatchMode.Exact)),
                (1, Endpoint("header", new[] { "abc", "def" }, HeaderValueMatchMode.Exact)),
                (1, Endpoint("header2", new[] { "abc", "def" }, HeaderValueMatchMode.Exact)),

                (2, Endpoint("header", new[] { "abc" }, HeaderValueMatchMode.Prefix, caseSensitive: true)),

                (3, Endpoint("header", new[] { "abc" }, HeaderValueMatchMode.Prefix)),
                (3, Endpoint("header", new[] { "abc", "def" }, HeaderValueMatchMode.Prefix)),
                (3, Endpoint("header2", new[] { "abc", "def" }, HeaderValueMatchMode.Prefix)),

                (9, Endpoint("header", new string[0], HeaderValueMatchMode.Exact, caseSensitive: true)),
                (9, Endpoint("header", new string[0], HeaderValueMatchMode.Exact)),
                (9, Endpoint("header", new string[0], HeaderValueMatchMode.Prefix, caseSensitive: true)),
                (9, Endpoint("header", new string[0], HeaderValueMatchMode.Prefix)),
                (9, Endpoint("header", new string[0])),

                (10, Endpoint(string.Empty, null)),
                (10, Endpoint(null, null)),
            };
            var sut = new HeaderMatcherPolicy();

            // Act
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
            // Arrange
            var scenarios = new[]
            {
                Endpoint("org-id", new string[0]),
                Endpoint("org-id", new[] { "abc" }),
                Endpoint("org-id", new[] { "abc", "def" }),
                Endpoint(null, null, isDynamic: true),
                Endpoint(string.Empty, null, isDynamic: true),
                Endpoint("org-id", new string[0], isDynamic: true),
                Endpoint("org-id", new[] { "abc" }, isDynamic: true),
                Endpoint(null, null, isDynamic: true),
            };
            var sut = new HeaderMatcherPolicy();
            var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

            // Act
            for (var i = 0; i < scenarios.Length; i++)
            {
                var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { scenarios[i] });
                Assert.True(result, $"scenario {i}");
            }
        }

        [Fact]
        public void AppliesToEndpoints_DoesNotApplyScenarios()
        {
            // Arrange
            var scenarios = new[]
            {
                Endpoint(null, null),
                Endpoint(string.Empty, null),
                Endpoint(string.Empty, new string[0]),
                Endpoint(string.Empty, new[] { "abc" }),
            };
            var sut = new HeaderMatcherPolicy();
            var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

            // Act
            for (var i = 0; i < scenarios.Length; i++)
            {
                var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { scenarios[i] });
                Assert.False(result, $"scenario {i}");
            }
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", true)]
        [InlineData("abc", true)]
        public async Task ApplyAsync_MatchingScenarios_AnyHeaderValue(string incomingHeaderValue, bool shouldMatch)
        {
            // Arrange
            var context = new DefaultHttpContext();
            if (incomingHeaderValue != null)
            {
                context.Request.Headers.Add("org-id", incomingHeaderValue);
            }

            var endpoint = Endpoint("org-id", new string[0]);
            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            // Act
            await sut.ApplyAsync(context, candidates);

            // Assert
            Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
        }

        [Fact]
        public async Task ApplyAsync_MultipleHeaderValues_NotSupported()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers.Add("org-id", new[] { "a", "b" });

            var endpoint = Endpoint("org-id", new[] { "a" });
            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            // Act
            await sut.ApplyAsync(context, candidates);

            // Assert
            Assert.False(candidates.IsValidCandidate(0));
        }

        [Theory]
        [InlineData("abc", HeaderValueMatchMode.Exact, false, null, false)]
        [InlineData("abc", HeaderValueMatchMode.Exact, false, "", false)]
        [InlineData("abc", HeaderValueMatchMode.Exact, false, "abc", true)]
        [InlineData("abc", HeaderValueMatchMode.Exact, false, "aBC", true)]
        [InlineData("abc", HeaderValueMatchMode.Exact, false, "abcd", false)]
        [InlineData("abc", HeaderValueMatchMode.Exact, false, "ab", false)]
        [InlineData("abc", HeaderValueMatchMode.Exact, true, "", false)]
        [InlineData("abc", HeaderValueMatchMode.Exact, true, "abc", true)]
        [InlineData("abc", HeaderValueMatchMode.Exact, true, "aBC", false)]
        [InlineData("abc", HeaderValueMatchMode.Exact, true, "abcd", false)]
        [InlineData("abc", HeaderValueMatchMode.Exact, true, "ab", false)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, false, "", false)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, false, "abc", true)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, false, "aBC", true)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, false, "abcd", true)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, false, "ab", false)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, true, "", false)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, true, "abc", true)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, true, "aBC", false)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, true, "abcd", true)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, true, "aBCd", false)]
        [InlineData("abc", HeaderValueMatchMode.Prefix, true, "ab", false)]
        public async Task ApplyAsync_MatchingScenarios_OneHeaderValue(
            string headerValue,
            HeaderValueMatchMode headerValueMatchMode,
            bool caseSensitive,
            string incomingHeaderValue,
            bool shouldMatch)
        {
            // Arrange
            var context = new DefaultHttpContext();
            if (incomingHeaderValue != null)
            {
                context.Request.Headers.Add("org-id", incomingHeaderValue);
            }

            var endpoint = Endpoint("org-id", new[] { headerValue }, headerValueMatchMode, caseSensitive);
            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            // Act
            await sut.ApplyAsync(context, candidates);

            // Assert
            Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
        }

        [Theory]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, null, false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, "", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, "abc", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, "aBc", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, "abcd", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, "def", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, "deF", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, false, "defg", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, null, false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, "", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, "abc", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, "aBC", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, "aBCd", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, "def", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, "DEFg", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Exact, true, "dEf", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, null, false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "abc", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "aBc", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "abcd", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "abcD", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "def", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "deF", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "defg", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "defG", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, false, "aabc", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, null, false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, "", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, "abc", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, "aBC", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, "aBCd", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, "def", true)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, "DEFg", false)]
        [InlineData("abc", "def", HeaderValueMatchMode.Prefix, true, "aabc", false)]
        public async Task ApplyAsync_MatchingScenarios_TwoHeaderValues(
            string header1Value,
            string header2Value,
            HeaderValueMatchMode headerValueMatchMode,
            bool caseSensitive,
            string incomingHeaderValue,
            bool shouldMatch)
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers.Add("org-id", incomingHeaderValue);
            var endpoint = Endpoint("org-id", new[] { header1Value, header2Value }, headerValueMatchMode, caseSensitive);

            var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
            var sut = new HeaderMatcherPolicy();

            // Act
            await sut.ApplyAsync(context, candidates);

            // Assert
            Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
        }

        private static Endpoint Endpoint(
            string headerName,
            string[] headerValues,
            HeaderValueMatchMode headerValueMatchMode = HeaderValueMatchMode.Exact,
            bool caseSensitive = false,
            bool isDynamic = false)
        {
            var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
            var metadata = new Mock<IHeaderMetadata>();
            metadata.SetupGet(m => m.HeaderName).Returns(headerName);
            metadata.SetupGet(m => m.HeaderValues).Returns(headerValues);
            metadata.SetupGet(m => m.ValueMatchMode).Returns(headerValueMatchMode);
            metadata.SetupGet(m => m.CaseSensitive).Returns(caseSensitive);

            builder.Metadata.Add(metadata.Object);
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
