// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    public class ResponseTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly ResponseTransformFactory _factory = new ResponseTransformFactory();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformSuppressResponseHeaders(bool suppress)
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformSuppressResponseHeaders(suppress);

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            Assert.Equal(suppress, !builderContext.CopyResponseHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SuppressResponseHeaders(bool suppress)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.SuppressResponseHeaders(suppress);

            Assert.Equal(suppress, !builderContext.CopyResponseHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformSuppressResponseTrailers(bool suppress)
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformSuppressResponseTrailers(suppress);

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            Assert.Equal(suppress, !builderContext.CopyResponseTrailers);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SuppressResponseTrailers(bool suppress)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.SuppressResponseTrailers(suppress);

            Assert.Equal(suppress, !builderContext.CopyResponseTrailers);
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

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            ValidateResponseHeader(builderContext, append, always);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void AddResponseHeader(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddResponseHeader("name", "value", append, always);

            ValidateResponseHeader(builderContext, append, always);
        }

        private static void ValidateResponseHeader(TransformBuilderContext builderContext, bool append, bool always)
        {
            var responseTransform = Assert.Single(builderContext.ResponseTransforms);
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

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            ValidateResponseTrailer(builderContext, append, always);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void AddResponseTrailer(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddResponseTrailer("name", "value", append, always);

            ValidateResponseTrailer(builderContext, append, always);
        }

        private static void ValidateResponseTrailer(TransformBuilderContext builderContext, bool append, bool always)
        {
            var responseTransform = Assert.Single(builderContext.ResponseTrailersTransforms);
            var responseHeaderValueTransform = Assert.IsType<ResponseTrailerValueTransform>(responseTransform);
            Assert.Equal("name", responseHeaderValueTransform.HeaderName);
            Assert.Equal("value", responseHeaderValueTransform.Value);
            Assert.Equal(append, responseHeaderValueTransform.Append);
            Assert.Equal(always, responseHeaderValueTransform.Always);
        }
    }
}
