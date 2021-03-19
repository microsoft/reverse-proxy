// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Xunit;
using Yarp.ReverseProxy.Service.Config;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    public class RequestHeadersTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly RequestHeadersTransformFactory _factory = new();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithTransformCopyRequestHeaders(bool copy)
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformCopyRequestHeaders(copy);

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            Assert.Equal(copy, builderContext.CopyRequestHeaders);
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
