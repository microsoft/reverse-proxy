// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class PathStringTransformTests
    {
        [Theory]
        [InlineData("/foo", "Set", "/value", "/value")]
        [InlineData("/foo", "Set", "", "")]
        [InlineData("/foo", "Prefix", "/value", "/value/foo")]
        [InlineData("/value/foo", "RemovePrefix", "/value", "/foo")]
        public void Set_Path_Success(string initialValue, string modeString, string transformValue, string expected)
        {
            // We can't put an internal type in a public test API parameter.
            var mode = Enum.Parse<PathStringTransform.PathTransformMode>(modeString);
            var context = new RequestParametersTransformContext() { Path = initialValue };
            var transform = new PathStringTransform(mode, transformValue);
            transform.Apply(context);
            Assert.Equal(expected, context.Path);
        }
    }
}
