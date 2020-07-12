// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class QueryStringRemoveTransformTests
    {
        [Fact]
        public void RemovesExistingQueryStringParameter()
        {
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryString("?z=1")
            };
            var transform = new QueryStringRemoveTransform("z");
            transform.Apply(context);
            Assert.False(context.Query.HasValue);
        }

        [Fact]
        public void LeavesOtherQueryStringParameters()
        {
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryString("?z=1&a=2"),
            };
            var transform = new QueryStringRemoveTransform("z");
            transform.Apply(context);
            Assert.Equal("?a=2", context.Query.Value);
        }

        [Fact]
        public void DoesNotFailOnNonExistingQueryStringParameter()
        {
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryString("?z=1"),
            };
            var transform = new QueryStringRemoveTransform("a");
            transform.Apply(context);
            Assert.Equal("?z=1", context.Query.Value);
        }
    }
}
