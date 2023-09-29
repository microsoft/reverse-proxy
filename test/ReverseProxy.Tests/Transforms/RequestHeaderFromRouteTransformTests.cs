using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Xunit;
using Yarp.ReverseProxy.Transforms;

namespace Yarp.ReverseProxy.Tests.Transforms;

public class RequestHeaderFromRouteTransformTests
{
    [Theory]
    [InlineData("defaultHeader","value","/{a}/{b}/{c}", "a", "value;6", true)]
    [InlineData("defaultHeader","value","/{a}/{b}/{c}", "notInRoute", "value", true)]
    [InlineData("defaultHeader","value","/{a}/{b}/{c}", "notInRoute", "value", false)]
    [InlineData("defaultHeader","value","/{a}/{b}/{c}", "a", "6", false)]
    [InlineData("h1","value","/{a}/{b}/{c}", "a", "6", false)]
    [InlineData("h1","value","/{a}/{b}/{c}", "b", "7", false)]
    [InlineData("h1","value","/{a}/{*remainder}", "remainder", "7/8", false)]
    public async Task AddsRequestHeaderFromRouteValue_SetHeader(string headerName, string defaultHeaderStartValue, string pattern, string routeValueKey, string expected, bool append)
    {
        // Arrange
        const string path = "/6/7/8";

        var routeValues = new RouteValueDictionary();
        var templateMatcher = new TemplateMatcher(TemplateParser.Parse(pattern), new RouteValueDictionary());
        templateMatcher.TryMatch(path, routeValues);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = routeValues;
        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("defaultHeader", defaultHeaderStartValue.Split(";", StringSplitOptions.RemoveEmptyEntries));

        var context = new RequestTransformContext()
        {
            Path = path,
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            HeadersCopied = true
        };

        // Act
        var transform = new RequestHeaderFromRouteTransform(headerName, routeValueKey, append);
        await transform.ApplyAsync(context);

        // Assert
        Assert.Equal(expected.Split(";", StringSplitOptions.RemoveEmptyEntries), proxyRequest.Headers.GetValues(headerName));
    }
}
