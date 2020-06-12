// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
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
            var errorReporter = new TestConfigErrorReporter();

            var valid = transformBuilder.Validate(null, "routeId", errorReporter);
            Assert.True(valid);
            Assert.Empty(errorReporter.Errors);

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
            var errorReporter = new TestConfigErrorReporter();
            var transforms = new List<IDictionary<string, string>>();

            var valid = transformBuilder.Validate(transforms, "routeId", errorReporter);
            Assert.True(valid);
            Assert.Empty(errorReporter.Errors);

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
            var errorReporter = new TestConfigErrorReporter();
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

            var valid = transformBuilder.Validate(transforms, "routeId", errorReporter);
            Assert.True(valid);
            Assert.Empty(errorReporter.Errors);

            var results = transformBuilder.Build(transforms);
            Assert.NotNull(results);
            Assert.Null(results.CopyRequestHeaders);
            Assert.Empty(results.ResponseHeaderTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);
            Assert.Empty(results.RequestTransforms);
            Assert.Empty(results.RequestHeaderTransforms);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyRequestHeader(bool copyRequestHeaders)
        {
            var transformBuilder = CreateTransformBuilder();
            var errorReporter = new TestConfigErrorReporter();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeadersCopy",  copyRequestHeaders.ToString() }
                }
            };

            var valid = transformBuilder.Validate(transforms, "routeId", errorReporter);
            Assert.True(valid);
            Assert.Empty(errorReporter.Errors);

            var results = transformBuilder.Build(transforms);
            Assert.NotNull(results);
            Assert.Equal(copyRequestHeaders, results.CopyRequestHeaders);
        }

        [Fact]
        public void EmptyTransform_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var errorReporter = new TestConfigErrorReporter();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), // Empty
            };

            var valid = transformBuilder.Validate(transforms, "routeId", errorReporter);
            Assert.False(valid);
            Assert.Single(errorReporter.Errors);
            var error = errorReporter.Errors.First();
            Assert.Equal("routeId", error.ElementId);
            Assert.Equal("Unknown transform: ", error.Message);

            var nie = Assert.Throws<NotSupportedException>(() => transformBuilder.Build(transforms));
            Assert.Equal("", nie.Message);
        }

        [Fact]
        public void UnknownTransforms_Error()
        {
            var transformBuilder = CreateTransformBuilder();
            var errorReporter = new TestConfigErrorReporter();
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

            var valid = transformBuilder.Validate(transforms, "routeId", errorReporter);
            Assert.False(valid);
            // All errors reported
            Assert.Equal(2, errorReporter.Errors.Count);
            Assert.Equal("Unknown transform: string1;string2", errorReporter.Errors.First().Message);
            Assert.Equal("Unknown transform: string3;string4", errorReporter.Errors.Skip(1).First().Message);

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
            var errorReporter = new TestConfigErrorReporter();
            var transforms = new List<IDictionary<string, string>>()
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeader",  "HeaderName" },
                    {  append ? "Append" : "Set",  value }
                }
            };

            var valid = transformBuilder.Validate(transforms, "routeId", errorReporter);
            Assert.True(valid);
            Assert.Empty(errorReporter.Errors);

            var results = transformBuilder.Build(transforms);
            Assert.Equal(1, results.RequestHeaderTransforms.Count);
            Assert.IsType<RequestHeaderValueTransform>(results.RequestHeaderTransforms["heaDername"]);
            // TODO: How to check Append/Set and the value?
        }

        private ITransformBuilder CreateTransformBuilder()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.AddRouting();
            serviceCollection.AddSingleton<ITransformBuilder, TransformBuilder>();
            using var services = serviceCollection.BuildServiceProvider();
            return services.GetRequiredService<ITransformBuilder>();
        }
    }
}
