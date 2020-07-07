// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Utilities;
using Tests.Common;
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
        public void NullTransforms_Success()
        {
            var transformBuilder = CreateTransformBuilder();

            var valid = transformBuilder.Validate(null, "routeId");
            Assert.True(valid);

            var results = transformBuilder.Build(null);
            Assert.NotNull(results);
            Assert.Null(results.CopyRequestHeaders);
            Assert.Empty(results.ResponseHeaderTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);
            Assert.Empty(results.RequestTransforms);

            Assert.Equal(5, results.RequestHeaderTransforms.Count);
            Assert.IsType<RequestHeaderValueTransform>(results.RequestHeaderTransforms[HeaderNames.Host]);
            Assert.IsType<RequestHeaderXForwardedForTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedForHeaderName]);
            Assert.IsType<RequestHeaderXForwardedHostTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedHostHeaderName]);
            Assert.IsType<RequestHeaderXForwardedPathBaseTransform>(results.RequestHeaderTransforms["X-Forwarded-PathBase"]);
            Assert.IsType<RequestHeaderXForwardedProtoTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedProtoHeaderName]);
        }

        [Fact]
        public void EmptyTransforms_AddsDefaults()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>();

            var valid = transformBuilder.Validate(transforms, "routeId");
            Assert.True(valid);

            var results = transformBuilder.Build(transforms);
            Assert.NotNull(results);
            Assert.Null(results.CopyRequestHeaders);
            Assert.Empty(results.ResponseHeaderTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);
            Assert.Empty(results.RequestTransforms);

            Assert.Equal(5, results.RequestHeaderTransforms.Count);
            Assert.IsType<RequestHeaderValueTransform>(results.RequestHeaderTransforms[HeaderNames.Host]);
            Assert.IsType<RequestHeaderXForwardedForTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedForHeaderName]);
            Assert.IsType<RequestHeaderXForwardedHostTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedHostHeaderName]);
            Assert.IsType<RequestHeaderXForwardedPathBaseTransform>(results.RequestHeaderTransforms["X-Forwarded-PathBase"]);
            Assert.IsType<RequestHeaderXForwardedProtoTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedProtoHeaderName]);
        }

        [Fact]
        public void DefaultsCanBeDisabled()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
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

            var valid = transformBuilder.Validate(transforms, "routeId");
            Assert.True(valid);

            var results = transformBuilder.Build(transforms);
            Assert.NotNull(results);
            Assert.Null(results.CopyRequestHeaders);
            Assert.Empty(results.ResponseHeaderTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);
            Assert.Empty(results.RequestTransforms);
            Assert.Empty(results.RequestHeaderTransforms);
        }

        [Fact]
        public void DefaultsCanBeOverridenByForwarded()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
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

            var valid = transformBuilder.Validate(transforms, "routeId");
            Assert.True(valid);

            var results = transformBuilder.Build(transforms);
            Assert.NotNull(results);
            Assert.Null(results.CopyRequestHeaders);
            Assert.Empty(results.ResponseHeaderTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);
            Assert.Empty(results.RequestTransforms);

            Assert.Equal(1, results.RequestHeaderTransforms.Count);
            Assert.IsType<RequestHeaderForwardedTransform>(results.RequestHeaderTransforms["Forwarded"]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyRequestHeader(bool copyRequestHeaders)
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeadersCopy",  copyRequestHeaders.ToString() }
                }
            };

            var valid = transformBuilder.Validate(transforms, "routeId");
            Assert.True(valid);

            var results = transformBuilder.Build(transforms);
            Assert.NotNull(results);
            Assert.Equal(copyRequestHeaders, results.CopyRequestHeaders);
        }

        [Fact]
        public void EmptyTransform_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), // Empty
            };

            var valid = transformBuilder.Validate(transforms, "routeId");
            Assert.False(valid);

            var nie = Assert.Throws<NotSupportedException>(() => transformBuilder.Build(transforms));
            Assert.Equal("", nie.Message);
        }

        [Fact]
        public void UnknownTransforms_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) // Unrecognized transform
                {
                    {  "string1", "value1" },
                    {  "string2", "value2" }
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) // Unrecognized transform
                {
                    {  "string3", "value3" },
                    {  "string4", "value4" }
                },
            };

            var valid = transformBuilder.Validate(transforms, "routeId");
            Assert.False(valid);

            var nie = Assert.Throws<NotSupportedException>(() => transformBuilder.Build(transforms));
            // First error reported
            Assert.Equal("string1;string2", nie.Message);
        }

        [Theory]
        [InlineData(false, "")]
        [InlineData(true, "")]
        [InlineData(false, "value")]
        [InlineData(true, "value")]
        public void RequestHeader(bool append, string value)
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeader",  "HeaderName" },
                    {  append ? "Append" : "Set",  value }
                }
            };

            var valid = transformBuilder.Validate(transforms, "routeId");
            Assert.True(valid);

            var results = transformBuilder.Build(transforms);
            Assert.IsType<RequestHeaderValueTransform>(results.RequestHeaderTransforms["heaDerName"]);
            // TODO: How to check Append/Set and the value?
        }

        private ITransformBuilder CreateTransformBuilder()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.AddRouting();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<ITransformBuilder, TransformBuilder>();
            serviceCollection.AddSingleton<IRandomFactory, RandomFactory>();
            using var services = serviceCollection.BuildServiceProvider();
            return services.GetRequiredService<ITransformBuilder>();
        }
    }
}
