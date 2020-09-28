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
                (0, Endpoint("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, caseSensitive: true)),

                (1, Endpoint("header", new[] { "abc" }, HeaderMatchMode.ExactHeader)),
                (1, Endpoint("header", new[] { "abc", "def" }, HeaderMatchMode.ExactHeader)),
                (1, Endpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.ExactHeader)),

                (2, Endpoint("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix, caseSensitive: true)),

                (3, Endpoint("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix)),
                (3, Endpoint("header", new[] { "abc", "def" }, HeaderMatchMode.HeaderPrefix)),
                (3, Endpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.HeaderPrefix)),

                (9, Endpoint("header", new string[0], HeaderMatchMode.ExactHeader, caseSensitive: true)),
                (9, Endpoint("header", new string[0], HeaderMatchMode.ExactHeader)),
                (9, Endpoint("header", new string[0], HeaderMatchMode.HeaderPrefix, caseSensitive: true)),
                (9, Endpoint("header", new string[0], HeaderMatchMode.HeaderPrefix)),
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
        [InlineData("", false)]
        [InlineData("abc", true)]
        public async Task ApplyAsync_MatchingScenarios_AnyHeaderValue(string incomingHeaderValue, bool shouldMatch)
        {
            // Arrange
            var context = new DefaultHttpContext();
            if (incomingHeaderValue != null)
            {
                context.Request.Headers.Add("org-id", incomingHeaderValue);
            }

            var endpoint = Endpoint("org-id", new[] { string.Empty }, HeaderMatchMode.Exists);
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
            var metadata1 = new HeaderMetadata("header1", new[] { "value1" }, HeaderMatchMode.ExactHeader, caseSensitive: false);
            var metadata2 = new HeaderMetadata("header2", new[] { "value2" }, HeaderMatchMode.ExactHeader, caseSensitive: false);
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
            HeaderMatchMode headerValueMatchMode = HeaderMatchMode.ExactHeader,
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
