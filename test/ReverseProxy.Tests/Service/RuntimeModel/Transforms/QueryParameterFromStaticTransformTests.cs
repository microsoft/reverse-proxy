// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryParameterFromStaticTransformTests
    {
        [Fact]
        public async Task Append_AddsQueryStringParameterWithStaticValue()
        {
            var httpContext = new DefaultHttpContext();
            var context = new RequestTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "foo");
            await transform.ApplyAsync(context);
            Assert.Equal("?z=foo", context.Query.QueryString.Value);
        }

        [Fact]
        public async Task Append_IgnoresExistingQueryStringParameter()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?z=1");
            var context = new RequestTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "foo");
            await transform.ApplyAsync(context);
            Assert.Equal("?z=1&z=foo", context.Query.QueryString.Value);
        }

        [Fact]
        public async Task Set_OverwritesExistingQueryStringParameter()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?z=1");
            var context = new RequestTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "foo");
            await transform.ApplyAsync(context);
            Assert.Equal("?z=foo", context.Query.QueryString.Value);
        }

        [Fact]
        public async Task Set_AddsNewQueryStringParameter()
        {
            var httpContext = new DefaultHttpContext();
            var context = new RequestTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "foo");
            await transform.ApplyAsync(context);
            Assert.Equal("?z=foo", context.Query.QueryString.Value);
        }
    }
}
