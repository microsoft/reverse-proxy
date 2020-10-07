// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RemoveQueryParameterTransformTests
    {
        [Fact]
        public void RemovesExistingQueryParameter()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?z=1");
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request)
            };
            var transform = new QueryParameterRemoveTransform("z");
            transform.Apply(context);
            Assert.False(context.Query.QueryString.HasValue);
        }

        [Fact]
        public void LeavesOtherQueryParameters()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?z=1&a=2");
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
            };
            var transform = new QueryParameterRemoveTransform("z");
            transform.Apply(context);
            Assert.Equal("?a=2", context.Query.QueryString.Value);
        }

        [Fact]
        public void DoesNotFailOnNonExistingQueryParameter()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?z=1");
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryTransformContext(httpContext.Request),
            };
            var transform = new QueryParameterRemoveTransform("a");
            transform.Apply(context);
            Assert.Equal("?z=1", context.Query.QueryString.Value);
        }
    }
}
