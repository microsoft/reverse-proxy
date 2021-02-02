// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    public class TransformBuilderContextFuncExtensionsTests : TransformExtentionsTestsBase
    {
        [Fact]
        public void AddRequestTransform()
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddRequestTransform(context =>
            {
                return Task.CompletedTask;
            });

            var requestTransform = Assert.Single(builderContext.RequestTransforms);
            Assert.IsType<RequestFuncTransform>(requestTransform);
        }

        [Fact]
        public void AddResponseTransform()
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddResponseTransform(context =>
            {
                return Task.CompletedTask;
            });

            var responseTransform = Assert.Single(builderContext.ResponseTransforms);
            Assert.IsType<ResponseFuncTransform>(responseTransform);
        }

        [Fact]
        public void AddResponseTrailersTransform()
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddResponseTrailersTransform(context =>
            {
                return Task.CompletedTask;
            });

            var responseTrailersTransform = Assert.Single(builderContext.ResponseTrailersTransforms);
            Assert.IsType<ResponseTrailersFuncTransform>(responseTrailersTransform);
        }
    }
}
