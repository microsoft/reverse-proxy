// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Config
{
    public class ProxyRouteTransformExtensionsTests
    {
        [Fact]
        public void WithTransformSuppressResponseHeaders()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute = proxyRoute.WithTransformSuppressResponseHeaders();

            var transform = BuildTransform(proxyRoute);

            Assert.False(transform.ShouldCopyResponseHeaders);
        }

        [Fact]
        public void WithTransformSuppressResponseTrailers()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute = proxyRoute.WithTransformSuppressResponseTrailers();

            var transform = BuildTransform(proxyRoute);

            Assert.False(transform.ShouldCopyResponseTrailers);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void WithTransformResponseHeader(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute = proxyRoute.WithTransformResponseHeader("name", "value", append, always);

            var transform = BuildTransform(proxyRoute);

            var responseTransform = Assert.Single(transform.ResponseTransforms);
            var responseHeaderValueTransform = Assert.IsType<ResponseHeaderValueTransform>(responseTransform);
            Assert.Equal("name", responseHeaderValueTransform.HeaderName);
            Assert.Equal("value", responseHeaderValueTransform.Value);
            Assert.Equal(append, responseHeaderValueTransform.Append);
            Assert.Equal(always, responseHeaderValueTransform.Always);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void WithTransformResponseTrailer(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute = proxyRoute.WithTransformResponseTrailer("name", "value", append, always);

            var transform = BuildTransform(proxyRoute);

            var responseTransform = Assert.Single(transform.ResponseTrailerTransforms);
            var responseHeaderValueTransform = Assert.IsType<ResponseTrailerValueTransform>(responseTransform);
            Assert.Equal("name", responseHeaderValueTransform.HeaderName);
            Assert.Equal("value", responseHeaderValueTransform.Value);
            Assert.Equal(append, responseHeaderValueTransform.Append);
            Assert.Equal(always, responseHeaderValueTransform.Always);
        }

        private static StructuredTransformer BuildTransform(ProxyRoute proxyRoute)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddReverseProxy();
            var services = serviceCollection.BuildServiceProvider();

            var builder = (TransformBuilder)services.GetRequiredService<ITransformBuilder>();

            return builder.BuildInternal(proxyRoute);
        }

        private static ProxyRoute CreateProxyRoute()
        {
            return new ProxyRoute
            {
                // With defaults turned off.
                Transforms = new List<IReadOnlyDictionary<string, string>>()
                {
                    new Dictionary<string, string>()
                    {
                        { "RequestHeaderOriginalHost", "true" }
                    },
                    new Dictionary<string, string>()
                    {
                        { "X-Forwarded", "" }
                    }
                }
            };
        }
    }
}
