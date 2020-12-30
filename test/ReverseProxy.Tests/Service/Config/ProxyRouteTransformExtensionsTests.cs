// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Config
{
    public class ProxyRouteTransformExtensionsTests
    {
        [Fact]
        public void AddTransformPathSet()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformPathSet(new PathString("/path#"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.Set, pathStringTransform.Mode);
            Assert.Equal("/path#", pathStringTransform.Value.Value);
        }

        [Fact]
        public void AddTransformPathRemovePrefix()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformPathRemovePrefix(new PathString("/path#"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.RemovePrefix, pathStringTransform.Mode);
            Assert.Equal("/path#", pathStringTransform.Value.Value);
        }

        [Fact]
        public void AddTransformPathPrefix()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformPathPrefix(new PathString("/path#"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.Prefix, pathStringTransform.Mode);
            Assert.Equal("/path#", pathStringTransform.Value.Value);
        }

        [Fact]
        public void AddTransformPathRouteValues()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformPathRouteValues(new PathString("/path#"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathRouteValuesTransform = Assert.IsType<PathRouteValuesTransform>(requestTransform);
            Assert.Equal("/path#", pathRouteValuesTransform.Template.TemplateText);
        }

        [Fact]
        public void AddTransformSuppressRequestHeaders()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformSuppressRequestHeaders();

            var transform = BuildTransform(proxyRoute);

            Assert.False(transform.ShouldCopyRequestHeaders);
        }

        [Fact]
        public void AddTransformUseOriginalHostHeader()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformUseOriginalHostHeader();

            var transform = BuildTransform(proxyRoute);

            Assert.Empty(transform.RequestTransforms.OfType<RequestHeaderValueTransform>().Where(x => x.Name == HeaderNames.Host));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddTransformRequestHeader(bool append)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformRequestHeader("name", "value", append);

            var transform = BuildTransform(proxyRoute);

            var requestHeaderValueTransform = Assert.Single(transform.RequestTransforms.OfType<RequestHeaderValueTransform>().Where(x => x.Name == "name"));
            Assert.Equal("value", requestHeaderValueTransform.Value);
            Assert.Equal(append, requestHeaderValueTransform.Append);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void AddTransformResponseHeader(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformResponseHeader("name", "value", append, always);

            var transform = BuildTransform(proxyRoute);

            var responseTransform = Assert.Single(transform.ResponseHeaderTransforms);
            Assert.Equal("name", responseTransform.Key);
            var responseHeaderValueTransform = Assert.IsType<ResponseHeaderValueTransform>(responseTransform.Value);
            Assert.Equal("value", responseHeaderValueTransform.Value);
            Assert.Equal(append, responseHeaderValueTransform.Append);
            Assert.Equal(always, responseHeaderValueTransform.Always);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void AddTransformResponseTrailer(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformResponseTrailer("name", "value", append, always);

            var transform = BuildTransform(proxyRoute);

            var responseTransform = Assert.Single(transform.ResponseTrailerTransforms);
            Assert.Equal("name", responseTransform.Key);
            var responseHeaderValueTransform = Assert.IsType<ResponseHeaderValueTransform>(responseTransform.Value);
            Assert.Equal("value", responseHeaderValueTransform.Value);
            Assert.Equal(append, responseHeaderValueTransform.Append);
            Assert.Equal(always, responseHeaderValueTransform.Always);
        }

        [Fact]
        public void AddTransformClientCert()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformClientCert("name");

            var transform = BuildTransform(proxyRoute);

            var certTransform = Assert.Single(transform.RequestTransforms.OfType<RequestHeaderClientCertTransform>());
            Assert.Equal("name", certTransform.Name);
        }

        [Theory]
        [InlineData(true, true, true, true, true, "Random", "Random")]
        [InlineData(true, true, true, true, false, "Random", "Random")]
        [InlineData(false, false, false, false, true, "Random", "Random")]
        [InlineData(false, false, false, false, false, "Random", "Random")]
        [InlineData(false, false, true, true, true, "Random", "Random")]
        [InlineData(false, false, true, true, false, "Random", "Random")]
        [InlineData(false, false, true, true, false, "None", "None")]
        [InlineData(false, false, true, true, false, "RandomAndPort", "RandomAndPort")]
        [InlineData(false, false, true, true, false, "Unknown", "Unknown")]
        [InlineData(false, false, true, true, false, "UnknownAndPort", "UnknownAndPort")]
        [InlineData(false, false, true, true, false, "Ip", "Ip")]
        [InlineData(false, false, true, true, false, "IpAndPort", "IpAndPort")]
        public void AddTransformForwarded(bool useFor, bool useHost, bool useProto, bool useBy, bool append, string forFormat, string byFormat)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformForwarded(useFor, useHost, useProto, useBy, append, forFormat, byFormat);

            var transform = BuildTransform(proxyRoute);

            if (useBy || useFor || useHost || useProto)
            {
                var requestHeaderForwardedTransform = Assert.Single(transform.RequestTransforms.OfType<RequestHeaderForwardedTransform>());
                Assert.Equal(append, requestHeaderForwardedTransform.Append);
                Assert.Equal(useHost, requestHeaderForwardedTransform.HostEnabled);
                Assert.Equal(useProto, requestHeaderForwardedTransform.ProtoEnabled);

                if (useBy)
                {
                    Assert.Equal(byFormat, requestHeaderForwardedTransform.ByFormat.ToString());
                }
                else
                {
                    Assert.Equal("None", requestHeaderForwardedTransform.ByFormat.ToString());
                }

                if (useFor)
                {
                    Assert.Equal(forFormat, requestHeaderForwardedTransform.ForFormat.ToString());
                }
                else
                {
                    Assert.Equal("None", requestHeaderForwardedTransform.ForFormat.ToString());
                }
            }
        }

        [Theory]
        [InlineData(false, false, false, false, false)]
        [InlineData(false, false, false, false, true)]
        [InlineData(true, true, true, true, false)]
        [InlineData(true, true, true, true, true)]
        [InlineData(true, true, false, false, true)]
        [InlineData(true, true, false, false, false)]
        public void AddTransformXForwarded(bool useFor, bool useHost, bool useProto, bool usePathBase, bool append)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformXForwarded("prefix-", useFor, useHost, useProto, usePathBase, append);

            var transform = BuildTransform(proxyRoute);

            if (useFor)
            {
                var requestHeaderXForwardedForTransform = Assert.Single(transform.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>());
                Assert.Equal("prefix-For", requestHeaderXForwardedForTransform.Name);
                Assert.Equal(append, requestHeaderXForwardedForTransform.Append);
            }
            else
            {
                Assert.Empty(transform.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>());
            }

            if (useHost)
            {
                var requestHeaderXForwardedHostTransform = Assert.Single(transform.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>());
                Assert.Equal("prefix-Host", requestHeaderXForwardedHostTransform.Name);
                Assert.Equal(append, requestHeaderXForwardedHostTransform.Append);
            }
            else
            {
                Assert.Empty(transform.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>());
            }

            if (useProto)
            {
                var requestHeaderXForwardedProtoTransform = Assert.Single(transform.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>());
                Assert.Equal("prefix-Proto", requestHeaderXForwardedProtoTransform.Name);
                Assert.Equal(append, requestHeaderXForwardedProtoTransform.Append);
            }
            else
            {
                Assert.Empty(transform.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>());
            }

            if (usePathBase)
            {
                var requestHeaderXForwardedPathBaseTransform = Assert.Single(transform.RequestTransforms.OfType<RequestHeaderXForwardedPathBaseTransform>());
                Assert.Equal("prefix-PathBase", requestHeaderXForwardedPathBaseTransform.Name);
                Assert.Equal(append, requestHeaderXForwardedPathBaseTransform.Append);
            }
            else
            {
                Assert.Empty(transform.RequestTransforms.OfType<RequestHeaderXForwardedPathBaseTransform>());
            }
        }

        [Fact]
        public void AddTransformHttpMethod()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformHttpMethod(HttpMethods.Put, HttpMethods.Post);

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var httpMethodTransform = Assert.IsType<HttpMethodTransform>(requestTransform);
            Assert.Equal(HttpMethod.Put, httpMethodTransform.FromMethod);
            Assert.Equal(HttpMethod.Post, httpMethodTransform.ToMethod);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AddTransformQueryRouteParameter(bool append)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformQueryRouteParameter("key", "value", append);

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var queryParameterRouteTransform = Assert.IsType<QueryParameterRouteTransform>(requestTransform);
            Assert.Equal("key", queryParameterRouteTransform.Key);
            Assert.Equal("value", queryParameterRouteTransform.RouteValueKey);
            var expectedMode = append ? QueryStringTransformMode.Append : QueryStringTransformMode.Set;
            Assert.Equal(expectedMode, queryParameterRouteTransform.Mode);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AddTransformQueryValueParameter(bool append)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformQueryValueParameter("key", "value", append);

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var queryParameterFromStaticTransform = Assert.IsType<QueryParameterFromStaticTransform>(requestTransform);
            Assert.Equal("key", queryParameterFromStaticTransform.Key);
            Assert.Equal("value", queryParameterFromStaticTransform.Value);
            var expectedMode = append ? QueryStringTransformMode.Append : QueryStringTransformMode.Set;
            Assert.Equal(expectedMode, queryParameterFromStaticTransform.Mode);
        }

        [Fact]
        public void AddTransformRemoveQueryParameter()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformRemoveQueryParameter("key");

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var removeQueryParameterTransform = Assert.IsType<QueryParameterRemoveTransform>(requestTransform);
            Assert.Equal("key", removeQueryParameterTransform.Key);
        }

        private static StructuredTransformer BuildTransform(ProxyRoute proxyRoute)
        {
            var builder = new TransformBuilder(NullTemplateBinderFactory.Instance, new TestRandomFactory());

            return builder.BuildInternal(proxyRoute.Transforms);
        }

        private static ProxyRoute CreateProxyRoute()
        {
            return new ProxyRoute
            {
                // With defaults turned off.
                Transforms = new List<IDictionary<string, string>>()
                {
                    new Dictionary<string, string>()
                    {
                        { "RequestHeaderOriginalHost", "true" }
                    },
                    new Dictionary<string, string>()
                    {
                        { "X-Forwarded", "" }
                    }
                }
            };
        }

        private class NullTemplateBinderFactory : TemplateBinderFactory
        {
            public static TemplateBinderFactory Instance = new NullTemplateBinderFactory();

            public override TemplateBinder Create(RoutePattern pattern)
            {
                return null;
            }

            public override TemplateBinder Create(RouteTemplate template, RouteValueDictionary defaults)
            {
                return null;
            }
        }
    }
}
