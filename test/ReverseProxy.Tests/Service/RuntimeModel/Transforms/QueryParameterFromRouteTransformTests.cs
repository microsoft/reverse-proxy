// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryParameterFromRouteTransformTests
    {
        [Theory]
        [InlineData("/{a}/{b}/{c}", "a", "?z=6")]
        [InlineData("/{a}/{b}/{c}", "c", "?z=8")]
        [InlineData("/{a}/{*remainder}", "remainder", "?z=7%2F8")]
        public void Append_AddsQueryParameterWithRouteValue(string pattern, string routeValueKey, string expected)
        {
            const string path = "/6/7/8";

            var routeValues = new AspNetCore.Routing.RouteValueDictionary();
            var templateMatcher = new TemplateMatcher(TemplateParser.Parse(pattern), new AspNetCore.Routing.RouteValueDictionary());
            templateMatcher.TryMatch(path, routeValues);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues = routeValues;
            var context = new RequestParametersTransformContext()
            {
                Path = path,
                HttpContext = httpContext
            };
            var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Append, "z", routeValueKey);
            transform.Apply(context);
            Assert.Equal(expected, context.Query.Value);
        }

        [Fact]
        public void Append_IgnoresExistingQueryParameter()
        {
            const string path = "/6/7/8";

            var routeValues = new AspNetCore.Routing.RouteValueDictionary();
            var templateMatcher = new TemplateMatcher(TemplateParser.Parse("/{a}/{b}/{c}"), new AspNetCore.Routing.RouteValueDictionary());
            templateMatcher.TryMatch(path, routeValues);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues = routeValues;
            var context = new RequestParametersTransformContext()
            {
                Path = path,
                Query = new QueryString("?z=1"),
                HttpContext = httpContext
            };
            var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Append, "z", "a");
            transform.Apply(context);
            Assert.Equal("?z=1&z=6", context.Query.Value);
        }

        [Fact]
        public void Set_OverwritesExistingQueryParameter()
        {
            const string path = "/6/7/8";

            var routeValues = new AspNetCore.Routing.RouteValueDictionary();
            var templateMatcher = new TemplateMatcher(TemplateParser.Parse("/{a}/{b}/{c}"), new AspNetCore.Routing.RouteValueDictionary());
            templateMatcher.TryMatch(path, routeValues);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues = routeValues;
            var context = new RequestParametersTransformContext()
            {
                Path = path,
                Query = new QueryString("?z=1"),
                HttpContext = httpContext
            };
            var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Set, "z", "a");
            transform.Apply(context);
            Assert.Equal("?z=6", context.Query.Value);
        }

        [Fact]
        public void Set_AddsNewQueryParameter()
        {
            const string path = "/6/7/8";

            var routeValues = new AspNetCore.Routing.RouteValueDictionary();
            var templateMatcher = new TemplateMatcher(TemplateParser.Parse("/{a}/{b}/{c}"), new AspNetCore.Routing.RouteValueDictionary());
            templateMatcher.TryMatch(path, routeValues);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues = routeValues;
            var context = new RequestParametersTransformContext()
            {
                Path = path,
                HttpContext = httpContext
            };
            var transform = new QueryParameterRouteTransform(QueryStringTransformMode.Set, "z", "a");
            transform.Apply(context);
            Assert.Equal("?z=6", context.Query.Value);
        }
    }
}
