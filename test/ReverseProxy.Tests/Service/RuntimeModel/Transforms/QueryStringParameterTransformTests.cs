// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryStringParameterTransformTests
    {
        [Theory]
        [InlineData("/{a}/{b}/{c}", "?z=%2F6%2F7%2F8")]
        [InlineData("/{a}/foo/{b}/{c}/{d}", "?z=%2F6%2Ffoo%2F7%2F8")] // Unknown value (d) dropped
        [InlineData("/{a}/foo/{b}", "?z=%2F6%2Ffoo%2F7")] // Extra values (c) dropped
        public void Append_Pattern_AddsQueryStringParameterWithRouteValues(string pattern, string expected)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.AddRouting();
            using var services = serviceCollection.BuildServiceProvider();

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues = new AspNetCore.Routing.RouteValueDictionary()
            {
                { "a", "6" },
                { "b", "7" },
                { "c", "8" },
            };
            var context = new RequestParametersTransformContext()
            {
                Path = "/",
                HttpContext = httpContext
            };
            var transform = new QueryStringParameterTransform(QueryStringTransformMode.Append, "z", pattern, services.GetRequiredService<TemplateBinderFactory>());
            transform.Apply(context);
            Assert.Equal(expected, context.Query.Value);
        }
    }
}
