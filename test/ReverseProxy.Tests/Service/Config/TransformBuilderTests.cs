// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions;
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
            NullOrEmptyTransforms_AddsDefaults(new List<IReadOnlyDictionary<string, string>>());
        }

        private void NullOrEmptyTransforms_AddsDefaults(IReadOnlyList<IReadOnlyDictionary<string, string>> transforms)
        {
            var transformBuilder = CreateTransformBuilder();

            var route = new ProxyRoute { Transforms = transforms };
            var errors = transformBuilder.Validate(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            Assert.NotNull(results);
            Assert.Null(results.ShouldCopyRequestHeaders);
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
            var errors = transformBuilder.Validate(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            Assert.NotNull(results);
            Assert.Null(results.ShouldCopyRequestHeaders);
            Assert.Empty(results.RequestTransforms);
            Assert.Empty(results.ResponseTransforms);
            Assert.Empty(results.ResponseTrailerTransforms);
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
            var errors = transformBuilder.Validate(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            var transform = Assert.Single(results.RequestTransforms);
            var forwardedTransform = Assert.IsType<RequestHeaderForwardedTransform>(transform);
            Assert.True(forwardedTransform.ProtoEnabled);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyRequestHeader(bool copyRequestHeaders)
        {
            var transformBuilder = CreateTransformBuilder();
            var transforms = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeadersCopy",  copyRequestHeaders.ToString() }
                },
            };

            var route = new ProxyRoute() { Transforms = transforms };
            var errors = transformBuilder.Validate(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            Assert.NotNull(results);
            Assert.Equal(copyRequestHeaders, results.ShouldCopyRequestHeaders);
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
            var errors = transformBuilder.Validate(route);
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
            var errors = transformBuilder.Validate(route);
            //All errors reported
            Assert.Equal(2, errors.Count);
            Assert.Equal("Unknown transform: string1;string2", errors.First().Message);
            Assert.Equal("Unknown transform: string3;string4", errors.Skip(1).First().Message);
            var ex = Assert.Throws<ArgumentException>(() => transformBuilder.BuildInternal(route));
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
            var transforms = new[]
            {
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {  "RequestHeader",  "HeaderName" },
                    {  append ? "Append" : "Set",  value }
                }
            };

            var route = new ProxyRoute() { Transforms = transforms };
            var errors = transformBuilder.Validate(route);
            Assert.Empty(errors);

            var results = transformBuilder.BuildInternal(route);
            var headerTransform = Assert.Single(results.RequestTransforms.OfType<RequestHeaderValueTransform>().Where(x => x.HeaderName == "HeaderName"));
            Assert.Equal(append, headerTransform.Append);
            Assert.Equal(value, headerTransform.Value);
        }

        private TransformBuilder CreateTransformBuilder()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddReverseProxy();
            using var services = serviceCollection.BuildServiceProvider();
            return (TransformBuilder)services.GetRequiredService<ITransformBuilder>();
        }
    }
}
