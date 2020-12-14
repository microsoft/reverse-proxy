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
            NullOrEmptyTransforms_AddsDefaults(new List<IDictionary<string, string>>());
        }

        private void NullOrEmptyTransforms_AddsDefaults(List<IDictionary<string, string>> transforms)
        {
            var transformBuilder = CreateTransformBuilder();

            var errors = transformBuilder.Validate(transforms);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(transforms);
            Assert.Equal(5, results.RequestHeaderTransforms.Count);
            Assert.IsType<RequestHeaderValueTransform>(results.RequestHeaderTransforms[HeaderNames.Host]);
            Assert.IsType<RequestHeaderXForwardedForTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedForHeaderName]);
            Assert.IsType<RequestHeaderXForwardedHostTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedHostHeaderName]);
            Assert.IsType<RequestHeaderXForwardedPathBaseTransform>(results.RequestHeaderTransforms["X-Forwarded-PathBase"]);
            Assert.IsType<RequestHeaderXForwardedProtoTransform>(results.RequestHeaderTransforms[ForwardedHeadersDefaults.XForwardedProtoHeaderName]);

            var adaptedResults = results.AdaptedTransforms;
            Assert.NotNull(adaptedResults);
            Assert.False(adaptedResults.CopyRequestHeaders); // Manual copy
            Assert.NotNull(adaptedResults.OnRequest);
            Assert.Null(adaptedResults.OnResponse);
            Assert.Null(adaptedResults.OnResponseTrailers);
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

            var errors = transformBuilder.Validate(transforms);
            Assert.Empty(errors);

            var results = transformBuilder.Build(transforms);
            Assert.NotNull(results);
            Assert.True(results.CopyRequestHeaders);
            Assert.Null(results.OnRequest);
            Assert.Null(results.OnResponse);
            Assert.Null(results.OnResponseTrailers);
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

            var errors = transformBuilder.Validate(transforms);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(transforms);
            Assert.Single(results.RequestHeaderTransforms);
            Assert.IsType<RequestHeaderForwardedTransform>(results.RequestHeaderTransforms["Forwarded"]);

            var adaptedResults = results.AdaptedTransforms;
            Assert.NotNull(adaptedResults);
            Assert.False(adaptedResults.CopyRequestHeaders);
            Assert.NotNull(adaptedResults.OnRequest);
            Assert.Null(adaptedResults.OnResponse);
            Assert.Null(adaptedResults.OnResponseTrailers);
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
                },
            };

            var errors = transformBuilder.Validate(transforms);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(transforms);
            Assert.NotNull(results);
            Assert.Equal(copyRequestHeaders, results.ShouldCopyRequestHeaders);


            var adaptedResults = results.AdaptedTransforms;
            // Only matches the input so long as there are no transforms (including defaults).
            // Always disabled when there are transforms because the copy is managed internally.
            Assert.False(adaptedResults.CopyRequestHeaders);
            Assert.NotNull(adaptedResults.OnRequest);
            Assert.Null(adaptedResults.OnResponse);
            Assert.Null(adaptedResults.OnResponseTrailers);
        }

        [Fact]
        public void EmptyTransform_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), // Empty
            };

            var errors = transformBuilder.Validate(transforms);
            var error = Assert.Single(errors);
            Assert.Equal("Unknown transform: ", error.Message);

            var nie = Assert.Throws<ArgumentException>(() => transformBuilder.Build(transforms));
            Assert.Equal("Unknown transform: ", nie.Message);
        }

        [Fact]
        public void UnknownTransforms_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new List<IDictionary<string, string>>()
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

            var errors = transformBuilder.Validate(transforms);
            //All errors reported
            Assert.Equal(2, errors.Count);
            Assert.Equal("Unknown transform: string1;string2", errors.First().Message);
            Assert.Equal("Unknown transform: string3;string4", errors.Skip(1).First().Message);
            var ex = Assert.Throws<ArgumentException>(() => transformBuilder.Build(transforms));
            // First error reported
            Assert.Equal("Unknown transform: string1;string2", ex.Message);
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

            var errors = transformBuilder.Validate(transforms);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(transforms);
            Assert.IsType<RequestHeaderValueTransform>(results.RequestHeaderTransforms["heaDerName"]);
            // TODO: How to check Append/Set and the value?

            var adaptedResults = results.AdaptedTransforms;
            Assert.False(adaptedResults.CopyRequestHeaders);
            Assert.NotNull(adaptedResults.OnRequest);
            Assert.Null(adaptedResults.OnResponse);
            Assert.Null(adaptedResults.OnResponseTrailers);
        }

        private TransformBuilder CreateTransformBuilder()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.AddRouting();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<ITransformBuilder, TransformBuilder>();
            serviceCollection.AddSingleton<IRandomFactory, RandomFactory>();
            using var services = serviceCollection.BuildServiceProvider();
            return (TransformBuilder)services.GetRequiredService<ITransformBuilder>();
        }
    }
}
