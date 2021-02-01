// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Utilities;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    public class ForwardedTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly ForwardedTransformFactory _factory = new ForwardedTransformFactory(new TestRandomFactory());

        [Theory]
        [InlineData(false, false, false, false, false)]
        [InlineData(false, false, false, false, true)]
        [InlineData(true, true, true, true, false)]
        [InlineData(true, true, true, true, true)]
        [InlineData(true, true, false, false, true)]
        [InlineData(true, true, false, false, false)]
        public void WithTransformXForwarded(bool useFor, bool useHost, bool useProto, bool usePathBase, bool append)
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformXForwarded("prefix-", useFor, useHost, useProto, usePathBase, append);

            var transformValues = Assert.Single(proxyRoute.Transforms);
            Validate(_factory, proxyRoute, transformValues);

            var builderContext = CreateBuilderContext(proxyRoute);
            Assert.True(_factory.Build(builderContext, transformValues));

            ValidateXForwarded(builderContext, useFor, useHost, useProto, usePathBase, append);
        }

        [Theory]
        [InlineData(false, false, false, false, false)]
        [InlineData(false, false, false, false, true)]
        [InlineData(true, true, true, true, false)]
        [InlineData(true, true, true, true, true)]
        [InlineData(true, true, false, false, true)]
        [InlineData(true, true, false, false, false)]
        public void AddXForwarded(bool useFor, bool useHost, bool useProto, bool usePathBase, bool append)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddXForwarded("prefix-", useFor, useHost, useProto, usePathBase, append);

            ValidateXForwarded(builderContext, useFor, useHost, useProto, usePathBase, append);
        }

        private void ValidateXForwarded(TransformBuilderContext builderContext, bool useFor, bool useHost, bool useProto, bool usePathBase, bool append)
        {
            Assert.False(builderContext.UseDefaultForwarders);

            if (useFor)
            {
                var requestHeaderXForwardedForTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>());
                Assert.Equal("prefix-For", requestHeaderXForwardedForTransform.HeaderName);
                Assert.Equal(append, requestHeaderXForwardedForTransform.Append);
            }
            else
            {
                Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>());
            }

            if (useHost)
            {
                var requestHeaderXForwardedHostTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>());
                Assert.Equal("prefix-Host", requestHeaderXForwardedHostTransform.HeaderName);
                Assert.Equal(append, requestHeaderXForwardedHostTransform.Append);
            }
            else
            {
                Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>());
            }

            if (useProto)
            {
                var requestHeaderXForwardedProtoTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>());
                Assert.Equal("prefix-Proto", requestHeaderXForwardedProtoTransform.HeaderName);
                Assert.Equal(append, requestHeaderXForwardedProtoTransform.Append);
            }
            else
            {
                Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>());
            }

            if (usePathBase)
            {
                var requestHeaderXForwardedPathBaseTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedPathBaseTransform>());
                Assert.Equal("prefix-PathBase", requestHeaderXForwardedPathBaseTransform.HeaderName);
                Assert.Equal(append, requestHeaderXForwardedPathBaseTransform.Append);
            }
            else
            {
                Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedPathBaseTransform>());
            }
        }

        [Theory]
        [InlineData(true, true, true, true, true, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(true, true, true, true, false, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, false, false, true, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, false, false, false, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, true, true, true, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, true, true, false, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, true, true, false, NodeFormat.None, NodeFormat.None)]
        [InlineData(false, false, true, true, false, NodeFormat.RandomAndPort, NodeFormat.RandomAndPort)]
        [InlineData(false, false, true, true, false, NodeFormat.Unknown, NodeFormat.Unknown)]
        [InlineData(false, false, true, true, false, NodeFormat.UnknownAndPort, NodeFormat.UnknownAndPort)]
        [InlineData(false, false, true, true, false, NodeFormat.Ip, NodeFormat.Ip)]
        [InlineData(false, false, true, true, false, NodeFormat.IpAndPort, NodeFormat.IpAndPort)]
        public void WithTransformForwarded(bool useFor, bool useHost, bool useProto, bool useBy, bool append, NodeFormat forFormat, NodeFormat byFormat)
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformForwarded(useFor, useHost, useProto, useBy, append, forFormat, byFormat);

            var transformValues = Assert.Single(proxyRoute.Transforms);
            Validate(_factory, proxyRoute, transformValues);

            var builderContext = CreateBuilderContext(proxyRoute, CreateServices());
            Assert.True(_factory.Build(builderContext, transformValues));

            ValidateForwarded(builderContext, useFor, useHost, useProto, useBy, append, forFormat, byFormat);
        }

        [Theory]
        [InlineData(true, true, true, true, true, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(true, true, true, true, false, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, false, false, true, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, false, false, false, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, true, true, true, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, true, true, false, NodeFormat.Random, NodeFormat.Random)]
        [InlineData(false, false, true, true, false, NodeFormat.None, NodeFormat.None)]
        [InlineData(false, false, true, true, false, NodeFormat.RandomAndPort, NodeFormat.RandomAndPort)]
        [InlineData(false, false, true, true, false, NodeFormat.Unknown, NodeFormat.Unknown)]
        [InlineData(false, false, true, true, false, NodeFormat.UnknownAndPort, NodeFormat.UnknownAndPort)]
        [InlineData(false, false, true, true, false, NodeFormat.Ip, NodeFormat.Ip)]
        [InlineData(false, false, true, true, false, NodeFormat.IpAndPort, NodeFormat.IpAndPort)]
        public void AddForwarded(bool useFor, bool useHost, bool useProto, bool useBy, bool append, NodeFormat forFormat, NodeFormat byFormat)
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute, CreateServices());
            builderContext.AddForwarded(useFor, useHost, useProto, useBy, append, forFormat, byFormat);

            ValidateForwarded(builderContext, useFor, useHost, useProto, useBy, append, forFormat, byFormat);
        }

        private void ValidateForwarded(TransformBuilderContext builderContext, bool useFor, bool useHost, bool useProto, bool useBy,
            bool append, NodeFormat forFormat, NodeFormat byFormat)
        {
            Assert.False(builderContext.UseDefaultForwarders);

            if (useBy || useFor || useHost || useProto)
            {
                var transform = Assert.Single(builderContext.RequestTransforms);
                var requestHeaderForwardedTransform = Assert.IsType<RequestHeaderForwardedTransform>(transform);
                Assert.Equal(append, requestHeaderForwardedTransform.Append);
                Assert.Equal(useHost, requestHeaderForwardedTransform.HostEnabled);
                Assert.Equal(useProto, requestHeaderForwardedTransform.ProtoEnabled);
                Assert.Equal(useBy ? byFormat : NodeFormat.None, requestHeaderForwardedTransform.ByFormat);
                Assert.Equal(useFor ? forFormat : NodeFormat.None, requestHeaderForwardedTransform.ForFormat);
            }
            else
            {
                Assert.Empty(builderContext.RequestTransforms);
            }
        }

        [Fact]
        public void WithTransformClientCertHeader()
        {
            var proxyRoute = CreateProxyRoute();
            proxyRoute = proxyRoute.WithTransformClientCertHeader("name");

            var transformValues = Assert.Single(proxyRoute.Transforms);
            Validate(_factory, proxyRoute, transformValues);

            var builderContext = CreateBuilderContext(proxyRoute);
            Assert.True(_factory.Build(builderContext, transformValues));
            var transform = Assert.Single(builderContext.RequestTransforms);

            var certTransform = Assert.IsType<RequestHeaderClientCertTransform>(transform);
            Assert.Equal("name", certTransform.HeaderName);
        }

        [Fact]
        public void AddClientCertHeader()
        {
            var proxyRoute = CreateProxyRoute();
            var builderContext = CreateBuilderContext(proxyRoute);
            builderContext.AddClientCertHeader("name");

            var transform = Assert.Single(builderContext.RequestTransforms);
            var certTransform = Assert.IsType<RequestHeaderClientCertTransform>(transform);
            Assert.Equal("name", certTransform.HeaderName);
        }

        private IServiceProvider CreateServices()
        {
            var collection = new ServiceCollection();
            collection.AddSingleton<IRandomFactory, TestRandomFactory>();
            return collection.BuildServiceProvider();
        }
    }
}
