using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public void AddPathSetTransform()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddPathSetTransform(new PathString("/path"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.Set, pathStringTransform.Mode);
            Assert.Equal(new PathString("/path"), pathStringTransform.Value);
        }

        [Fact]
        public void AddPathRemovePrefixTransform()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddPathRemovePrefixTransform(new PathString("/path"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.RemovePrefix, pathStringTransform.Mode);
            Assert.Equal(new PathString("/path"), pathStringTransform.Value);
        }

        [Fact]
        public void AddPathPrefixTransform()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddPathPrefixTransform(new PathString("/path"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
            Assert.Equal(PathStringTransform.PathTransformMode.Prefix, pathStringTransform.Mode);
            Assert.Equal(new PathString("/path"), pathStringTransform.Value);
        }

        [Fact]
        public void AddPathRouteValuesTransform()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddPathRouteValuesTransform(new PathString("/path"));

            var transform = BuildTransform(proxyRoute);

            var requestTransform = Assert.Single(transform.RequestTransforms);
            var pathRouteValuesTransform = Assert.IsType<PathRouteValuesTransform>(requestTransform);
            Assert.Equal("/path", pathRouteValuesTransform.Template.TemplateText);
        }

        [Fact]
        public void CopyRequestHeaders()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.CopyRequestHeaders();

            var transform = BuildTransform(proxyRoute);

            Assert.True(transform.CopyRequestHeaders);
        }

        [Fact]
        public void SuppressRequestHeaders()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.SuppressRequestHeaders();

            var transform = BuildTransform(proxyRoute);

            Assert.False(transform.CopyRequestHeaders);
        }

        [Fact]
        public void AddRequestHeaderOriginalHost()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddRequestHeaderOriginalHost();

            var transform = BuildTransform(proxyRoute);

            Assert.Empty(transform.RequestHeaderTransforms.Where(x => x.Key == HeaderNames.Host));
        }

        [Fact]
        public void SuppressRequestHeaderOriginalHost()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.SuppressRequestHeaderOriginalHost();

            var transform = BuildTransform(proxyRoute);

            var requestTransform = transform.RequestHeaderTransforms[HeaderNames.Host];
            var requestHeaderValueTransform = Assert.IsType<RequestHeaderValueTransform>(requestTransform);
            Assert.Equal(string.Empty, requestHeaderValueTransform.Value);
            Assert.False(requestHeaderValueTransform.Append);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddRequestHeaderTransform(bool append)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddRequestHeaderTransform("name", "value", append);

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
        public void AddResponseHeaderTransform(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddResponseHeaderTransform("name", "value", append, always);

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
        public void AddResponseTrailerTransform(bool append, bool always)
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddResponseTrailerTransform("name", "value", append, always);

            var transform = BuildTransform(proxyRoute);

            var responseTransform = Assert.Single(transform.ResponseTrailerTransforms);
            Assert.Equal("name", responseTransform.Key);
            var responseHeaderValueTransform = Assert.IsType<ResponseHeaderValueTransform>(responseTransform.Value);
            Assert.Equal("value", responseHeaderValueTransform.Value);
            Assert.Equal(append, responseHeaderValueTransform.Append);
            Assert.Equal(always, responseHeaderValueTransform.Always);
        }

        [Fact]
        public void AddClientCertTransform()
        {
            var proxyRoute = CreateProxyRoute();

            proxyRoute.AddClientCertTransform("name");

            var transform = BuildTransform(proxyRoute);

            var requestTransform = transform.RequestHeaderTransforms["name"];
            Assert.IsType<RequestHeaderClientCertTransform>(requestTransform);
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
