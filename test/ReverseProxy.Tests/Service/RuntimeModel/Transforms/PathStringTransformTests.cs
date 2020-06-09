// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class PathStringTransformTests
    {
        [Theory]
        [InlineData("/foo", PathStringTransform.PathTransformMode.Set, "/value", "/value")]
        [InlineData("/foo", PathStringTransform.PathTransformMode.Set, "", "")]
        [InlineData("/foo", PathStringTransform.PathTransformMode.Prefix, "/value", "/value/foo")]
        [InlineData("/value/foo", PathStringTransform.PathTransformMode.RemovePrefix, "/value", "/foo")]
        public void Set_Path_Success(string initialValue, PathStringTransform.PathTransformMode mode, string transformValue, string expected)
        {
            var context = new RequestParametersTransformContext() { Path = initialValue };
            var transform = new PathStringTransform(mode, transformValue);
            transform.Apply(context);
            Assert.Equal(expected, context.Path);
        }
    }
}
