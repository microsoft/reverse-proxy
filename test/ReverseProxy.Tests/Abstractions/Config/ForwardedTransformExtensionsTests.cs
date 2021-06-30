// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.Service.Config;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Abstractions.Config
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
        public void WithTransformXForwarded(bool useFor, bool useHost, bool useProto, bool usePrefix, bool append)
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformXForwarded("prefix-", useFor, useHost, useProto, usePrefix, append);

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            ValidateXForwarded(builderContext, useFor, useHost, useProto, usePrefix, append);
        }

        [Theory]
        [InlineData(false, false, false, false, false)]
        [InlineData(false, false, false, false, true)]
        [InlineData(true, true, true, true, false)]
        [InlineData(true, true, true, true, true)]
        [InlineData(true, true, false, false, true)]
        [InlineData(true, true, false, false, false)]
        public void AddXForwarded(bool useFor, bool useHost, bool useProto, bool usePrefix, bool append)
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddXForwarded("prefix-", useFor, useHost, useProto, usePrefix, append);

            ValidateXForwarded(builderContext, useFor, useHost, useProto, usePrefix, append);
        }

        private static void ValidateXForwarded(TransformBuilderContext builderContext, bool useFor, bool useHost, bool useProto, bool usePrefix, bool append)
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

            if (usePrefix)
            {
                var requestHeaderXForwardedPrefixTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedPrefixTransform>());
                Assert.Equal("prefix-Prefix", requestHeaderXForwardedPrefixTransform.HeaderName);
                Assert.Equal(append, requestHeaderXForwardedPrefixTransform.Append);
            }
            else
            {
                Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedPrefixTransform>());
            }
        }

        [Theory]
        [InlineData(NodeFormat.Random, true, true, NodeFormat.Random, true)]
        [InlineData(NodeFormat.RandomAndPort, true, true, NodeFormat.Random, false)]
        [InlineData(NodeFormat.None, false, false, NodeFormat.None, true)]
        [InlineData(NodeFormat.None, false, false, NodeFormat.None, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Random, true)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Random, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.None, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.RandomAndPort, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Unknown, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.UnknownAndPort, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Ip, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.IpAndPort, false)]
        public void WithTransformForwarded(NodeFormat forFormat, bool useHost, bool useProto, NodeFormat byFormat, bool append)
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformForwarded(useHost, useProto, forFormat, byFormat, append);

            var builderContext = ValidateAndBuild(proxyRoute, _factory, CreateServices());

            ValidateForwarded(builderContext, useHost, useProto, forFormat, byFormat, append);
        }

        [Theory]
        [InlineData(NodeFormat.Random, true, true, NodeFormat.Random, true)]
        [InlineData(NodeFormat.RandomAndPort, true, true, NodeFormat.Random, false)]
        [InlineData(NodeFormat.None, false, false, NodeFormat.None, true)]
        [InlineData(NodeFormat.None, false, false, NodeFormat.None, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Random, true)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Random, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.None, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.RandomAndPort, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Unknown, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.UnknownAndPort, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.Ip, false)]
        [InlineData(NodeFormat.None, false, true, NodeFormat.IpAndPort, false)]
        public void AddForwarded(NodeFormat forFormat, bool useHost, bool useProto, NodeFormat byFormat, bool append)
        {
            var builderContext = CreateBuilderContext(services: CreateServices());
            builderContext.AddForwarded(useHost, useProto, forFormat, byFormat, append);

            ValidateForwarded(builderContext, useHost, useProto, forFormat, byFormat, append);
        }

        private static void ValidateForwarded(TransformBuilderContext builderContext, bool useHost, bool useProto,
            NodeFormat forFormat, NodeFormat byFormat, bool append)
        {
            Assert.False(builderContext.UseDefaultForwarders);

            if (byFormat != NodeFormat.None|| forFormat != NodeFormat.None || useHost || useProto)
            {
                var transform = Assert.Single(builderContext.RequestTransforms);
                var requestHeaderForwardedTransform = Assert.IsType<RequestHeaderForwardedTransform>(transform);
                Assert.Equal(append, requestHeaderForwardedTransform.Append);
                Assert.Equal(useHost, requestHeaderForwardedTransform.HostEnabled);
                Assert.Equal(useProto, requestHeaderForwardedTransform.ProtoEnabled);
                Assert.Equal(byFormat, requestHeaderForwardedTransform.ByFormat);
                Assert.Equal(forFormat, requestHeaderForwardedTransform.ForFormat);
            }
            else
            {
                Assert.Empty(builderContext.RequestTransforms);
            }
        }

        [Fact]
        public void WithTransformClientCertHeader()
        {
            var proxyRoute = new ProxyRoute();
            proxyRoute = proxyRoute.WithTransformClientCertHeader("name");

            var builderContext = ValidateAndBuild(proxyRoute, _factory);

            var transform = Assert.Single(builderContext.RequestTransforms);
            var certTransform = Assert.IsType<RequestHeaderClientCertTransform>(transform);
            Assert.Equal("name", certTransform.HeaderName);
        }

        [Fact]
        public void AddClientCertHeader()
        {
            var builderContext = CreateBuilderContext();
            builderContext.AddClientCertHeader("name");

            var transform = Assert.Single(builderContext.RequestTransforms);
            var certTransform = Assert.IsType<RequestHeaderClientCertTransform>(transform);
            Assert.Equal("name", certTransform.HeaderName);
        }

        private static IServiceProvider CreateServices()
        {
            var collection = new ServiceCollection();
            collection.AddSingleton<IRandomFactory, TestRandomFactory>();
            return collection.BuildServiceProvider();
        }
    }
}
