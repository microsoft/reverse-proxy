// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Yarp.ReverseProxy.Discovery;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms.Tests
{
    public class ResponseTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly ResponseTransformFactory _factory = new();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformCopyResponseHeaders(bool copy)
        {
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformCopyResponseHeaders(copy);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            Assert.Equal(copy, builderContext.CopyResponseHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformCopyResponseTrailers(bool copy)
        {
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformCopyResponseTrailers(copy);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            Assert.Equal(copy, builderContext.CopyResponseTrailers);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void WithTransformResponseHeader(bool append, bool always)
        {
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformResponseHeader("name", "value", append, always);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            ValidateResponseHeader(builderContext, append, always);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void AddResponseHeader(bool append, bool always)
        {
            var builderContext = CreateBuilderContext();
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
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformResponseTrailer("name", "value", append, always);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            ValidateResponseTrailer(builderContext, append, always);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void AddResponseTrailer(bool append, bool always)
        {
            var builderContext = CreateBuilderContext();
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
