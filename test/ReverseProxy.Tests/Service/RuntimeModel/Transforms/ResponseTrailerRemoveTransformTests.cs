// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;
using Yarp.ReverseProxy.Utilities.Tests;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class ResponseTrailerRemoveTransformTests
    {
        [Theory]
        [InlineData("header1", "value1", 200, false, "header1", "")]
        [InlineData("header1", "value1", 404, false, "header1", "header1")]
        [InlineData("header1", "value1", 200, true, "header1", "")]
        [InlineData("header1", "value1", 404, true, "header1", "")]
        [InlineData("header1", "value1", 200, false, "headerX", "header1")]
        [InlineData("header1", "value1", 404, false, "headerX", "header1")]
        [InlineData("header1", "value1", 200, true, "headerX", "header1")]
        [InlineData("header1", "value1", 404, true, "headerX", "header1")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 200, false, "header2", "header1; header3")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 404, false, "header2", "header1; header2; header3")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 200, true, "header2", "header1; header3")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 404, true, "header2", "header1; header3")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 200, false, "headerX", "header1; header2; header3")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 404, false, "headerX", "header1; header2; header3")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 200, true, "headerX", "header1; header2; header3")]
        [InlineData("header1; header2; header3", "value1, value2, value3", 404, true, "headerX", "header1; header2; header3")]
        [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 200, false, "header2", "header1; header3")]
        [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 404, false, "header2", "header1; header2; header3")]
        [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 200, true, "header2", "header1; header3")]
        [InlineData("header1; header2; header2; header3", "value1, value2-1, value2-2, value3", 404, true, "header2", "header1; header3")]
        public async Task RemoveTrailerFromFeature_Success(string names, string values, int status, bool always, string removedHeader, string expected)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.StatusCode = status;
            var trailerFeature = new TestTrailersFeature();
            httpContext.Features.Set<IHttpResponseTrailersFeature>(trailerFeature);
            var proxyResponse = new HttpResponseMessage();
            foreach (var pair in TestResources.ParseNameAndValues(names, values))
            {
                trailerFeature.Trailers.Add(pair.Name, pair.Values);
            }

            var transform = new ResponseTrailerRemoveTransform(removedHeader, always);
            await transform.ApplyAsync(new ResponseTrailersTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = proxyResponse,
                HeadersCopied = true,
            });

            var expectedHeaders = expected.Split("; ", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(expectedHeaders, trailerFeature.Trailers.Select(h => h.Key));
        }

        private class TestTrailersFeature : IHttpResponseTrailersFeature
        {
            public IHeaderDictionary Trailers { get; set; } = new HeaderDictionary();
        }
    }
}
