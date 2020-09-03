// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryParameterFromStaticTransformTests
    {
        [Fact]
        public void Append_AddsQueryStringParameterWithStaticValue()
        {
            var httpContext = new DefaultHttpContext();
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "foo");
            transform.Apply(context);
            Assert.Equal("?z=foo", context.Query.QueryString.Value);
        }

        [Fact]
        public void Append_IgnoresExistingQueryStringParameter()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?z=1");
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Append, "z", "foo");
            transform.Apply(context);
            Assert.Equal("?z=1,foo", context.Query.QueryString.Value);
        }

        [Fact]
        public void Set_OverwritesExistingQueryStringParameter()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?z=1");
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "foo");
            transform.Apply(context);
            Assert.Equal("?z=foo", context.Query.QueryString.Value);
        }

        [Fact]
        public void Set_AddsNewQueryStringParameter()
        {
            var httpContext = new DefaultHttpContext();
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
                HttpContext = httpContext
            };
            var transform = new QueryParameterFromStaticTransform(QueryStringTransformMode.Set, "z", "foo");
            transform.Apply(context);
            Assert.Equal("?z=foo", context.Query.QueryString.Value);
        }
    }
}
