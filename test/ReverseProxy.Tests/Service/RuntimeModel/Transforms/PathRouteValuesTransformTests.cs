// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class PathRouteValuesTransformTests
    {
        [Theory]
        [InlineData("/{a}/{b}/{c}", "/6/7/8")]
        [InlineData("/{a}/foo/{b}/{c}/{d}", "/6/foo/7/8")] // Unknown value (d) dropped
        [InlineData("/{a}/foo/{b}", "/6/foo/7")] // Extra values (c) dropped
        public void Set_PathPattern_ReplacesPathWithRouteValues(string transformValue, string expected)
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
            var transform = new PathRouteValuesTransform(transformValue, services.GetRequiredService<TemplateBinderFactory>());
            transform.Apply(context);
            Assert.Equal(expected, context.Path);
        }
    }
}
