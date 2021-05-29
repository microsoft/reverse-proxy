// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    public class TransformBuilderContextFuncExtensionsTests : TransformExtentionsTestsBase
    {
        [Fact]
        public void AddRequestTransform()
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddRequestTransform(context =>
            {
                return default;
            });

            var requestTransform = Assert.Single(builderContext.RequestTransforms);
            Assert.IsType<RequestFuncTransform>(requestTransform);
        }

        [Fact]
        public void AddResponseTransform()
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddResponseTransform(context =>
            {
                return default;
            });

            var responseTransform = Assert.Single(builderContext.ResponseTransforms);
            Assert.IsType<ResponseFuncTransform>(responseTransform);
        }

        [Fact]
        public void AddResponseTrailersTransform()
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddResponseTrailersTransform(context =>
            {
                return default;
            });

            var responseTrailersTransform = Assert.Single(builderContext.ResponseTrailersTransforms);
            Assert.IsType<ResponseTrailersFuncTransform>(responseTrailersTransform);
        }
    }
}
