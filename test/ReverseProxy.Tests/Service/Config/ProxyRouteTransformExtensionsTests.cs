using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common;
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
            Assert.Equal("/path#", pathStringTransform.Value);
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
            Assert.Equal("/path#", pathStringTransform.Value);
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
            Assert.Equal("/path#", pathStringTransform.Value);
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

            Assert.False(transform.CopyRequestHeaders);
        }

        [Fact]
        public void AddTransformUseOriginalHostHeader()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformUseOriginalHostHeader();

            var transform = BuildTransform(proxyRoute);

            Assert.Empty(transform.RequestHeaderTransforms.Where(x => x.Key == HeaderNames.Host));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddTransformRequestHeader(bool append)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformRequestHeader("name", "value", append);

            var transform = BuildTransform(proxyRoute);

            var requestTransform = transform.RequestHeaderTransforms["name"];
            var requestHeaderValueTransform = Assert.IsType<RequestHeaderValueTransform>(requestTransform);
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

            var requestTransform = transform.RequestHeaderTransforms["name"];
            Assert.IsType<RequestHeaderClientCertTransform>(requestTransform);
        }

        [Theory]
        [MemberData(nameof(AllForwardedCombinations))]
        public void AddTransformForwarded(bool useFor, bool useHost, bool useProto, bool useBy, bool append, string forFormat, string byFormat)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformForwarded(useFor, useHost, useProto, useBy, append, forFormat, byFormat);

            var transform = BuildTransform(proxyRoute);

            if (useBy || useFor || useHost || useProto)
            {
                var requestTransform = transform.RequestHeaderTransforms["Forwarded"];
                var requestHeaderForwardedTransform = Assert.IsType<RequestHeaderForwardedTransform>(requestTransform);
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

        public static IEnumerable<object[]> AllForwardedCombinations => GenerateForwardedCombinations();

        private static IEnumerable<object[]> GenerateForwardedCombinations()
        {
            var boolCollection = new object[] { true, false };
            var formatCollection = Enum.GetNames(typeof(RequestHeaderForwardedTransform.NodeFormat));

            for (var x0 = 0; x0 < boolCollection.Length; x0++)
            {
                for (var x1 = 0; x1 < boolCollection.Length; x1++)
                {
                    for (var x2 = 0; x2 < boolCollection.Length; x2++)
                    {
                        for (var x3 = 0; x3 < boolCollection.Length; x3++)
                        {
                            for (var x4 = 0; x4 < boolCollection.Length; x4++)
                            {
                                for (var x5 = 0; x5 < formatCollection.Length; x5++)
                                {
                                    for (var x6 = 0; x6 < formatCollection.Length; x6++)
                                    {
                                        yield return new object[]
                                        {
                                            boolCollection[x0],
                                            boolCollection[x1],
                                            boolCollection[x2],
                                            boolCollection[x3],
                                            boolCollection[x4],
                                            formatCollection[x5],
                                            formatCollection[x6],
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllXForwardedCombinations))]
        public void AddTransformXForwarded(bool useFor, bool useHost, bool useProto, bool usePathBase, bool append)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddTransformXForwarded("prefix-", useFor, useHost, useProto, usePathBase, append);

            var transform = BuildTransform(proxyRoute);

            if (useFor)
            {
                var requestTransform = transform.RequestHeaderTransforms["prefix-For"];
                var requestHeaderXForwardedForTransform = Assert.IsType<RequestHeaderXForwardedForTransform>(requestTransform);
                Assert.Equal(append, requestHeaderXForwardedForTransform.Append);
            }
            else
            {
                Assert.False(transform.RequestHeaderTransforms.TryGetValue("prefix-For", out _));
            }

            if (useHost)
            {
                var requestTransform = transform.RequestHeaderTransforms["prefix-Host"];
                var requestHeaderXForwardedHostTransform = Assert.IsType<RequestHeaderXForwardedHostTransform>(requestTransform);
                Assert.Equal(append, requestHeaderXForwardedHostTransform.Append);
            }
            else
            {
                Assert.False(transform.RequestHeaderTransforms.TryGetValue("prefix-Host", out _));
            }

            if (useProto)
            {
                var requestTransform = transform.RequestHeaderTransforms["prefix-Proto"];
                var requestHeaderXForwardedProtoTransform = Assert.IsType<RequestHeaderXForwardedProtoTransform>(requestTransform);
                Assert.Equal(append, requestHeaderXForwardedProtoTransform.Append);
            }
            else
            {
                Assert.False(transform.RequestHeaderTransforms.TryGetValue("prefix-Proto", out _));
            }

            if (usePathBase)
            {
                var requestTransform = transform.RequestHeaderTransforms["prefix-PathBase"];
                var requestHeaderXForwardedPathBaseTransform = Assert.IsType<RequestHeaderXForwardedPathBaseTransform>(requestTransform);
                Assert.Equal(append, requestHeaderXForwardedPathBaseTransform.Append);
            }
            else
            {
                Assert.False(transform.RequestHeaderTransforms.TryGetValue("prefix-PathBase", out _));
            }
        }

        public static IEnumerable<object[]> AllXForwardedCombinations => GenerateXForwarderCombinations();

        private static IEnumerable<object[]> GenerateXForwarderCombinations()
        {
            var boolCollection = new object[] { true, false };

            for (var x0 = 0; x0 < boolCollection.Length; x0++)
            {
                for (var x1 = 0; x1 < boolCollection.Length; x1++)
                {
                    for (var x2 = 0; x2 < boolCollection.Length; x2++)
                    {
                        for (var x3 = 0; x3 < boolCollection.Length; x3++)
                        {
                            for (var x4 = 0; x4 < boolCollection.Length; x4++)
                            {
                                yield return new object[]
                                {
                                    boolCollection[x0],
                                    boolCollection[x1],
                                    boolCollection[x2],
                                    boolCollection[x3],
                                    boolCollection[x4],
                                };
                            }
                        }
                    }
                }
            }
        }

        private static Transforms BuildTransform(ProxyRoute proxyRoute)
        {
            var builder = new TransformBuilder(NullTemplateBinderFactory.Instance, new TestRandomFactory(), NullLogger<TransformBuilder>.Instance);

            var transform = builder.Build(proxyRoute.Transforms);
            return transform;
        }

        private static ProxyRoute CreateProxyRoute()
        {
            return new ProxyRoute
            {
                Transforms = new List<IDictionary<string, string>>()
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
