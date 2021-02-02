// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
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
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformSuppressRequestHeaders(suppress);

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            Assert.Equal(suppress, !builderContext.CopyRequestHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SuppressRequestHeaders(bool suppress)
        {
            var builderContext = CreateBuilderContext();
            builderContext.SuppressRequestHeaders(suppress);

            Assert.Equal(suppress, !builderContext.CopyRequestHeaders);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformUseOriginalHostHeader(bool useOriginal)
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformUseOriginalHostHeader(useOriginal);

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            Assert.Equal(useOriginal, builderContext.UseOriginalHost);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddOriginalHostHeader(bool useOriginal)
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddOriginalHostHeader(useOriginal);

            Assert.Equal(useOriginal, builderContext.UseOriginalHost);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformRequestHeader(bool append)
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformRequestHeader("name", "value", append);

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

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

        private static void ValidateRequestHeader(bool append, TransformBuilderContext builderContext)
        {
            var requestHeaderValueTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderValueTransform>().Where(x => x.HeaderName == "name"));
            Assert.Equal("value", requestHeaderValueTransform.Value);
            Assert.Equal(append, requestHeaderValueTransform.Append);
        }
    }
}
