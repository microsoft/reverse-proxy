// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Config
{
    public class TransformBuilderTests
    {
        [Fact]
        public void CreateBuilder_Success()
        {
            CreateTransformBuilder();
        }

        [Fact]
        public void NullTransforms_AddsDefaults()
        {
            NullOrEmptyTransforms_AddsDefaults(null);
        }

        [Fact]
        public void EmptyTransforms_AddsDefaults()
        {
            NullOrEmptyTransforms_AddsDefaults(new List<IReadOnlyDictionary<string, string>>());
        }

        private void NullOrEmptyTransforms_AddsDefaults(IReadOnlyList<IReadOnlyDictionary<string, string>> transforms)
        {
            var transformBuilder = CreateTransformBuilder();

            var route = new ProxyRoute { Transforms = transforms };
            var errors = transformBuilder.ValidateRoute(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            Assert.NotNull(results);
            Assert.Null(results.ShouldCopyRequestHeaders);
            Assert.Null(results.ShouldCopyResponseHeaders);
            Assert.Null(results.ShouldCopyResponseTrailers);
            Assert.Empty(results.ResponseTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);

            Assert.Equal(5, results.RequestTransforms.Count);
            var hostTransform = Assert.Single(results.RequestTransforms.OfType<RequestHeaderValueTransform>());
            Assert.Equal(HeaderNames.Host, hostTransform.HeaderName);
            Assert.Equal(string.Empty, hostTransform.Value);
            var forTransform = Assert.Single(results.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>());
            Assert.Equal(ForwardedHeadersDefaults.XForwardedForHeaderName, forTransform.HeaderName);
            var xHostTransform = Assert.Single(results.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>());
            Assert.Equal(ForwardedHeadersDefaults.XForwardedHostHeaderName, xHostTransform.HeaderName);
            var pathBaseTransform = Assert.Single(results.RequestTransforms.OfType<RequestHeaderXForwardedPathBaseTransform>());
            Assert.Equal("X-Forwarded-PathBase", pathBaseTransform.HeaderName);
            var protoTransform = Assert.Single(results.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>());
            Assert.Equal(ForwardedHeadersDefaults.XForwardedProtoHeaderName, protoTransform.HeaderName);
        }

        [Fact]
        public void EmptyTransform_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), // Empty
            };

            var route = new ProxyRoute() { Transforms = transforms };
            var errors = transformBuilder.ValidateRoute(route);
            var error = Assert.Single(errors);
            Assert.Equal("Unknown transform: ", error.Message);

            var nie = Assert.Throws<ArgumentException>(() => transformBuilder.BuildInternal(route));
            Assert.Equal("Unknown transform: ", nie.Message);
        }

        [Fact]
        public void UnknownTransforms_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) // Unknown transform
                {
                    {  "string1", "value1" },
                    {  "string2", "value2" }
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) // Unknown transform
                {
                    {  "string3", "value3" },
                    {  "string4", "value4" }
                },
            };

            var route = new ProxyRoute() { Transforms = transforms };
            var errors = transformBuilder.ValidateRoute(route);
            //All errors reported
            Assert.Equal(2, errors.Count);
            Assert.Equal("Unknown transform: string1;string2", errors.First().Message);
            Assert.Equal("Unknown transform: string3;string4", errors.Skip(1).First().Message);
            var ex = Assert.Throws<ArgumentException>(() => transformBuilder.BuildInternal(route));
            // First error reported
            Assert.Equal("Unknown transform: string1;string2", ex.Message);
        }

        [Fact]
        public void CallsTransformFactories()
        {
            var factory1 = new TestTransformFactory("1");
            var factory2 = new TestTransformFactory("2");
            var factory3 = new TestTransformFactory("3");
            var builder = new TransformBuilder(new ServiceCollection().BuildServiceProvider(),
                new[] { factory1, factory2, factory3 }, Array.Empty<ITransformProvider>());

            var route = new ProxyRoute().WithTransform(transform =>
            {
                transform["2"] = "B";
            });
            var errors = builder.ValidateRoute(route);
            Assert.Empty(errors);
            Assert.Equal(1, factory1.ValidationCalls);
            Assert.Equal(1, factory2.ValidationCalls);
            Assert.Equal(0, factory3.ValidationCalls);

            var transforms = builder.BuildInternal(route);
            Assert.Equal(1, factory1.BuildCalls);
            Assert.Equal(1, factory2.BuildCalls);
            Assert.Equal(0, factory3.BuildCalls);

            Assert.Single(transforms.ResponseTrailerTransforms);
        }

        [Fact]
        public void CallsTransformProviders()
        {
            var provider1 = new TestTransformProvider();
            var provider2 = new TestTransformProvider();
            var provider3 = new TestTransformProvider();
            var builder = new TransformBuilder(new ServiceCollection().BuildServiceProvider(),
                Array.Empty<ITransformFactory>(), new[] { provider1, provider2, provider3 });

            var route = new ProxyRoute();
            var errors = builder.ValidateRoute(route);
            Assert.Empty(errors);
            Assert.Equal(1, provider1.ValidationCalls);
            Assert.Equal(1, provider2.ValidationCalls);
            Assert.Equal(1, provider3.ValidationCalls);

            var transforms = builder.BuildInternal(route);
            Assert.Equal(1, provider1.ApplyCalls);
            Assert.Equal(1, provider2.ApplyCalls);
            Assert.Equal(1, provider3.ApplyCalls);

            Assert.Equal(3, transforms.ResponseTrailerTransforms.Count);
        }

        [Fact]
        public void DefaultsCanBeDisabled()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeaderOriginalHost", "true" }
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "X-Forwarded", "" }
                },
            };

            var route = new ProxyRoute() { Transforms = transforms };
            var errors = transformBuilder.ValidateRoute(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            Assert.NotNull(results);
            Assert.Null(results.ShouldCopyRequestHeaders);
            Assert.Empty(results.RequestTransforms);
            Assert.Empty(results.ResponseTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void UseOriginalHost(bool useOriginalHost, bool copyHeaders)
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeaderOriginalHost", useOriginalHost.ToString() }
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeadersCopy", copyHeaders.ToString() }
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "X-Forwarded", "" }
                },
            };

            var route = new ProxyRoute() { Transforms = transforms };
            var errors = transformBuilder.ValidateRoute(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            Assert.NotNull(results);
            Assert.Equal(copyHeaders, results.ShouldCopyRequestHeaders);
            Assert.Empty(results.ResponseTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);

            if (useOriginalHost && !copyHeaders)
            {
                var transform = Assert.Single(results.RequestTransforms);
                Assert.IsType<RequestCopyHostTransform>(transform);
            }
            else if (!useOriginalHost && copyHeaders)
            {
                var transform = Assert.Single(results.RequestTransforms);
                var headerTransform = Assert.IsType<RequestHeaderValueTransform>(transform);
                Assert.Equal(HeaderNames.Host, headerTransform.HeaderName);
                Assert.Equal(string.Empty, headerTransform.Value);
                Assert.False(headerTransform.Append);
            }
            else
            {
                Assert.Empty(results.RequestTransforms);
            }
        }

        [Fact]
        public void DefaultsCanBeOverridenByForwarded()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeaderOriginalHost", "true" }
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "Forwarded", "proto" }
                },
            };

            var route = new ProxyRoute() { Transforms = transforms };
            var errors = transformBuilder.ValidateRoute(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            var transform = Assert.Single(results.RequestTransforms);
            var forwardedTransform = Assert.IsType<RequestHeaderForwardedTransform>(transform);
            Assert.True(forwardedTransform.ProtoEnabled);
        }

        private static TransformBuilder CreateTransformBuilder()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddReverseProxy();
            using var services = serviceCollection.BuildServiceProvider();
            return (TransformBuilder)services.GetRequiredService<ITransformBuilder>();
        }

        private class TestTransformFactory : ITransformFactory
        {
            private readonly string _v;

            public int ValidationCalls { get; set; }
            public int BuildCalls { get; set; }

            public TestTransformFactory(string v)
            {
                _v = v;
            }

            public bool Validate(TransformValidationContext context, IReadOnlyDictionary<string, string> transformValues)
            {
                Assert.NotNull(context.Services);
                Assert.NotNull(context.Route);
                Assert.NotNull(context.Errors);
                ValidationCalls++;
                return transformValues.TryGetValue(_v, out var _);
            }

            public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
            {
                Assert.NotNull(context.Services);
                Assert.NotNull(context.Route);
                BuildCalls++;
                if (transformValues.TryGetValue(_v, out var _))
                {
                    context.AddResponseTrailersTransform(context => default);
                    return true;
                }

                return false;
            }
        }

        private class TestTransformProvider : ITransformProvider
        {
            public int ValidationCalls { get; set; }
            public int ApplyCalls { get; set; }

            public void ValidateRoute(TransformValidationContext context)
            {
                Assert.NotNull(context.Services);
                Assert.NotNull(context.Route);
                Assert.NotNull(context.Errors);
                ValidationCalls++;
            }

            public void Apply(TransformBuilderContext context)
            {
                Assert.NotNull(context.Services);
                Assert.NotNull(context.Route);
                ApplyCalls++;
                context.AddResponseTrailer("key", "value");
            }
        }
    }
}
