// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
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
                (0, Endpoint("header", new[] { "abc" }, HeaderMatchMode.Exact, caseSensitive: true)),

                (1, Endpoint("header", new[] { "abc" }, HeaderMatchMode.Exact)),
                (1, Endpoint("header", new[] { "abc", "def" }, HeaderMatchMode.Exact)),
                (1, Endpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.Exact)),

                (2, Endpoint("header", new[] { "abc" }, HeaderMatchMode.Prefix, caseSensitive: true)),

                (3, Endpoint("header", new[] { "abc" }, HeaderMatchMode.Prefix)),
                (3, Endpoint("header", new[] { "abc", "def" }, HeaderMatchMode.Prefix)),
                (3, Endpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.Prefix)),

                (9, Endpoint("header", new string[0], HeaderMatchMode.Exact, caseSensitive: true)),
                (9, Endpoint("header", new string[0], HeaderMatchMode.Exact)),
                (9, Endpoint("header", new string[0], HeaderMatchMode.Prefix, caseSensitive: true)),
                (9, Endpoint("header", new string[0], HeaderMatchMode.Prefix)),
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
        public void AppliesToEndpoints_NoMetadata_DoesNotApply()
        {
            var endpoint = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0).Build();

            var sut = new HeaderMatcherPolicy();
            var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

            var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { endpoint });
            Assert.False(result);
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
        [InlineData("abc", HeaderMatchMode.Exact, false, null, false)]
        [InlineData("abc", HeaderMatchMode.Exact, false, "", false)]
        [InlineData("abc", HeaderMatchMode.Exact, false, "abc", true)]
        [InlineData("abc", HeaderMatchMode.Exact, false, "aBC", true)]
        [InlineData("abc", HeaderMatchMode.Exact, false, "abcd", false)]
        [InlineData("abc", HeaderMatchMode.Exact, false, "ab", false)]
        [InlineData("abc", HeaderMatchMode.Exact, true, "", false)]
        [InlineData("abc", HeaderMatchMode.Exact, true, "abc", true)]
        [InlineData("abc", HeaderMatchMode.Exact, true, "aBC", false)]
        [InlineData("abc", HeaderMatchMode.Exact, true, "abcd", false)]
        [InlineData("abc", HeaderMatchMode.Exact, true, "ab", false)]
        [InlineData("abc", HeaderMatchMode.Prefix, false, "", false)]
        [InlineData("abc", HeaderMatchMode.Prefix, false, "abc", true)]
        [InlineData("abc", HeaderMatchMode.Prefix, false, "aBC", true)]
        [InlineData("abc", HeaderMatchMode.Prefix, false, "abcd", true)]
        [InlineData("abc", HeaderMatchMode.Prefix, false, "ab", false)]
        [InlineData("abc", HeaderMatchMode.Prefix, true, "", false)]
        [InlineData("abc", HeaderMatchMode.Prefix, true, "abc", true)]
        [InlineData("abc", HeaderMatchMode.Prefix, true, "aBC", false)]
        [InlineData("abc", HeaderMatchMode.Prefix, true, "abcd", true)]
        [InlineData("abc", HeaderMatchMode.Prefix, true, "aBCd", false)]
        [InlineData("abc", HeaderMatchMode.Prefix, true, "ab", false)]
        public async Task ApplyAsync_MatchingScenarios_OneHeaderValue(
            string headerValue,
            HeaderMatchMode headerValueMatchMode,
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
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, "aBc", true)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, "abcd", false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, "deF", true)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, false, "defg", false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, "aBC", false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, "aBCd", false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, "DEFg", false)]
        [InlineData("abc", "def", HeaderMatchMode.Exact, true, "dEf", false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "aBc", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "abcd", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "abcD", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "deF", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "defg", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "defG", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, false, "aabc", false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, null, false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, "", false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, "abc", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, "aBC", false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, "aBCd", false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, "def", true)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, "DEFg", false)]
        [InlineData("abc", "def", HeaderMatchMode.Prefix, true, "aabc", false)]
        public async Task ApplyAsync_MatchingScenarios_TwoHeaderValues(
            string header1Value,
            string header2Value,
            HeaderMatchMode headerValueMatchMode,
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

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public async Task ApplyAsync_MultipleRules_RequiresAllHeaders(bool sendHeader1, bool sendHeader2, bool shouldMatch)
        {
            var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
            var metadata1 = new HeaderMetadata("header1", new[] { "value1" }, HeaderMatchMode.Exact, caseSensitive: false);
            var metadata2 = new HeaderMetadata("header2", new[] { "value2" }, HeaderMatchMode.Exact, caseSensitive: false);
            builder.Metadata.Add(metadata1);
            builder.Metadata.Add(metadata2);
            var endpoint = builder.Build();

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

        private static Endpoint Endpoint(
            string headerName,
            string[] headerValues,
            HeaderMatchMode headerValueMatchMode = HeaderMatchMode.Exact,
            bool caseSensitive = false,
            bool isDynamic = false)
        {
            var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
            var metadata = new Mock<IHeaderMetadata>();
            metadata.SetupGet(m => m.HeaderName).Returns(headerName);
            metadata.SetupGet(m => m.HeaderValues).Returns(headerValues);
            metadata.SetupGet(m => m.Mode).Returns(headerValueMatchMode);
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
