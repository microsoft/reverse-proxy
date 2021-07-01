// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms.Tests
{
    public class RequestHeadersTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly RequestHeadersTransformFactory _factory = new();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformCopyRequestHeaders(bool copy)
        {
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformCopyRequestHeaders(copy);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            Assert.Equal(copy, builderContext.CopyRequestHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformUseOriginalHostHeader(bool useOriginal)
        {
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformUseOriginalHostHeader(useOriginal);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            var transform = Assert.Single(builderContext.RequestTransforms);
            var hostTransform = Assert.IsType<RequestHeaderOriginalHostTransform>(transform);

            Assert.Equal(useOriginal, hostTransform.UseOriginalHost);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformRequestHeader(bool append)
        {
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformRequestHeader("name", "value", append);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            ValidateRequestHeader(append, builderContext);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddRequestHeader(bool append)
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddRequestHeader("name", "value", append);

            ValidateRequestHeader(append, builderContext);
        }

        [Fact]
        public void RemoveRequestHeader()
        {
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformRequestHeaderRemove("MyHeader");

            ValidateAndBuild(routeConfig, _factory);   
        }

        private static void ValidateRequestHeader(bool append, TransformBuilderContext builderContext)
        {
            var requestHeaderValueTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderValueTransform>().Where(x => x.HeaderName == "name"));
            Assert.Equal("value", requestHeaderValueTransform.Value);
            Assert.Equal(append, requestHeaderValueTransform.Append);
        }
    }
}
