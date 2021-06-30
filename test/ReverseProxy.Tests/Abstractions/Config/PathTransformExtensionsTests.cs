// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Service.Config;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    public class PathTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly PathTransformFactory _factory;

        public PathTransformExtensionsTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.AddRouting();
            var services = serviceCollection.BuildServiceProvider();

            _factory = new PathTransformFactory(services.GetRequiredService<TemplateBinderFactory>());
        }

        [Fact]
        public void WithTransformPathSet()
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformPathSet(new PathString("/path#"));

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            ValidatePathSet(builderContext);
        }

        [Fact]
        public void AddPathSet()
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddPathSet(new PathString("/path#"));

            ValidatePathSet(builderContext);
        }

        private static void ValidatePathSet(TransformBuilderContext builderContext)
        {
            var requestTransform = Assert.Single(builderContext.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.Set, pathStringTransform.Mode);
            Assert.Equal("/path#", pathStringTransform.Value.Value);
        }

        [Fact]
        public void WithTransformPathRemovePrefix()
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformPathRemovePrefix(new PathString("/path#"));

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            ValidatePathRemovePrefix(builderContext);
        }

        [Fact]
        public void AddPathRemovePrefix()
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddPathRemovePrefix(new PathString("/path#"));

            ValidatePathRemovePrefix(builderContext);
        }

        private static void ValidatePathRemovePrefix(TransformBuilderContext builderContext)
        {
            var requestTransform = Assert.Single(builderContext.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.RemovePrefix, pathStringTransform.Mode);
            Assert.Equal("/path#", pathStringTransform.Value.Value);
        }

        [Fact]
        public void WithTransformPathPrefix()
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformPathPrefix(new PathString("/path#"));

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            ValidatePathPrefix(builderContext);
        }

        [Fact]
        public void AddPathPrefix()
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddPathPrefix(new PathString("/path#"));

            ValidatePathPrefix(builderContext);
        }

        private static void ValidatePathPrefix(TransformBuilderContext builderContext)
        {
            var requestTransform = Assert.Single(builderContext.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.Prefix, pathStringTransform.Mode);
            Assert.Equal("/path#", pathStringTransform.Value.Value);
        }

        [Fact]
        public void WithTransformPathRouteValues()
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformPathRouteValues(new PathString("/path#"));

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            ValidatePathRouteValues(builderContext);
        }

        [Fact]
        public void AddPathRouteValues()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.AddRouting();
            var services = serviceCollection.BuildServiceProvider();

            var builderContext = CreateBuilderContext(services: services);
            builderContext.AddPathRouteValues(new PathString("/path#"));

            ValidatePathRouteValues(builderContext);
        }

        private static void ValidatePathRouteValues(TransformBuilderContext builderContext)
        {
            var requestTransform = Assert.Single(builderContext.RequestTransforms);
            var pathRouteValuesTransform = Assert.IsType<PathRouteValuesTransform>(requestTransform);
            Assert.Equal("/path#", pathRouteValuesTransform.Template.TemplateText);
        }
    }
}
