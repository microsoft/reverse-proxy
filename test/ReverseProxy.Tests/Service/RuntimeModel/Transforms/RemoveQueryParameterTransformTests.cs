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
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryString("?z=1")
            };
            var transform = new RemoveQueryParameterTransform("z");
            transform.Apply(context);
            Assert.False(context.Query.HasValue);
        }

        [Fact]
        public void LeavesOtherQueryParameters()
        {
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryString("?z=1&a=2"),
            };
            var transform = new RemoveQueryParameterTransform("z");
            transform.Apply(context);
            Assert.Equal("?a=2", context.Query.Value);
        }

        [Fact]
        public void DoesNotFailOnNonExistingQueryParameter()
        {
            var context = new RequestParametersTransformContext()
            {
                Query = new QueryString("?z=1"),
            };
            var transform = new RemoveQueryParameterTransform("a");
            transform.Apply(context);
            Assert.Equal("?z=1", context.Query.Value);
        }
    }
}
