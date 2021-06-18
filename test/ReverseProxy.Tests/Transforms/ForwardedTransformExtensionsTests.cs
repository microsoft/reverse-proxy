// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms.Tests
{
    public class ForwardedTransformExtensionsTests : TransformExtentionsTestsBase
    {
        private readonly ForwardedTransformFactory _factory = new ForwardedTransformFactory(new TestRandomFactory());

        [Theory]
        [InlineData(false, false, false, false, ForwardedTransformActions.Set)]
        [InlineData(false, false, false, false, ForwardedTransformActions.Append)]
        [InlineData(true, true, true, true, ForwardedTransformActions.Set)]
        [InlineData(true, true, true, true, ForwardedTransformActions.Append)]
        [InlineData(true, true, false, false, ForwardedTransformActions.Append)]
        [InlineData(true, true, false, false, ForwardedTransformActions.Set)]
        public void WithTransformXForwarded(bool useFor, bool useHost, bool useProto, bool usePrefix, ForwardedTransformActions action)
        {
            var routeConfig = new RouteConfig();
            var prefix = "prefix-";
            routeConfig = routeConfig.WithTransformXForwarded(prefix, useFor, useHost, useProto, usePrefix, action);

            var builderContext = ValidateAndBuild(routeConfig, _factory);

            if (useFor)
            {
                ValidateXForwardedTransform("For", prefix, action, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>().Single());
            }

            if (useHost)
            {
                ValidateXForwardedTransform("Host", prefix, action, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>().Single());
            }

            if (useProto)
            {
                ValidateXForwardedTransform("Proto", prefix, action, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>().Single());
            }

            if (usePrefix)
            {
                ValidateXForwardedTransform("Prefix", prefix, action, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedPrefixTransform>().Single());
            }
        }

        [Theory]
        [MemberData(nameof(GetAddXForwardedCases))]
        public void AddXForwarded(Func<TransformBuilderContext, string, ForwardedTransformActions, TransformBuilderContext> addFunc,
            string transformName, ForwardedTransformActions action)
        {
            var builderContext = CreateBuilderContext();
            addFunc(builderContext, "prefix-", action);

            ValidateXForwarded(builderContext, transformName, "prefix-", action);
        }

        public static IEnumerable<object[]> GetAddXForwardedCases()
        {
            var actions = (ForwardedTransformActions[])Enum.GetValues(typeof(ForwardedTransformActions));
            var addTransformFuncs = new (Func<TransformBuilderContext, string, ForwardedTransformActions, TransformBuilderContext>, string)[]
            {
                (ForwardedTransformExtensions.AddXForwardedFor, "For"), (ForwardedTransformExtensions.AddXForwardedPrefix, "Prefix"),
                (ForwardedTransformExtensions.AddXForwardedHost, "Host"), (ForwardedTransformExtensions.AddXForwardedProto, "Proto")
            };

            return addTransformFuncs.Join(actions, _ => true, _ => true, (t, a) => new object[] { t.Item1, t.Item2, a });
        }

        private static void ValidateXForwarded(TransformBuilderContext builderContext, string transformName, string headerPrefix, ForwardedTransformActions action)
        {
            Assert.False(builderContext.UseDefaultForwarders);

            if (action == ForwardedTransformActions.Off)
            {
                Assert.Empty(builderContext.RequestTransforms);
            }
            else
            {
                var transform = Assert.Single(builderContext.RequestTransforms);
                ValidateXForwardedTransform(transformName, headerPrefix, action, transform);
            }
        }

        private static void ValidateXForwardedTransform(string transformName, string headerPrefix, ForwardedTransformActions action, RequestTransform transform)
        {
            Assert.Equal($"RequestHeaderXForwarded{transformName}Transform", transform.GetType().Name);
            Assert.Equal(headerPrefix + transformName, ((dynamic)transform).HeaderName);
            Assert.Equal(action, ((dynamic)transform).TransformAction);
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
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformForwarded(useHost, useProto, forFormat, byFormat, append);

            var builderContext = ValidateAndBuild(routeConfig, _factory, CreateServices());

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
            var routeConfig = new RouteConfig();
            routeConfig = routeConfig.WithTransformClientCertHeader("name");

            var builderContext = ValidateAndBuild(routeConfig, _factory);

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
