// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    public class RequestHeadersTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly RequestHeadersTransformFactory _factory = new RequestHeadersTransformFactory();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformSuppressRequestHeaders(bool suppress)
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformSuppressRequestHeaders(suppress);

            var transformValues = Assert.Single(proxyRoute.Transforms);
            Validate(_factory, proxyRoute, transformValues);

            var builderContext = CreateBuilderContext(proxyRoute);
            Assert.True(_factory.Build(builderContext, transformValues));

            Assert.Equal(suppress, !builderContext.CopyRequestHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SuppressRequestHeaders(bool suppress)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.SuppressRequestHeaders(suppress);

            Assert.Equal(suppress, !builderContext.CopyRequestHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformUseOriginalHostHeader(bool useOriginal)
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformUseOriginalHostHeader(useOriginal);

            var transformValues = Assert.Single(proxyRoute.Transforms);
            Validate(_factory, proxyRoute, transformValues);

            var builderContext = CreateBuilderContext(proxyRoute);
            Assert.True(_factory.Build(builderContext, transformValues));

            Assert.Equal(useOriginal, builderContext.UseOriginalHost);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddOriginalHostHeader(bool useOriginal)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddOriginalHostHeader(useOriginal);

            Assert.Equal(useOriginal, builderContext.UseOriginalHost);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformRequestHeader(bool append)
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformRequestHeader("name", "value", append);

            var transformValues = Assert.Single(proxyRoute.Transforms);
            Validate(_factory, proxyRoute, transformValues);

            var builderContext = CreateBuilderContext(proxyRoute);
            Assert.True(_factory.Build(builderContext, transformValues));

            ValidateRequestHeader(append, builderContext);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddRequestHeader(bool append)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddRequestHeader("name", "value", append);

            ValidateRequestHeader(append, builderContext);
        }

        private static void ValidateRequestHeader(bool append, TransformBuilderContext builderContext)
        {
            var requestHeaderValueTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderValueTransform>().Where(x => x.HeaderName == "name"));
            Assert.Equal("value", requestHeaderValueTransform.Value);
            Assert.Equal(append, requestHeaderValueTransform.Append);
        }
    }
}
