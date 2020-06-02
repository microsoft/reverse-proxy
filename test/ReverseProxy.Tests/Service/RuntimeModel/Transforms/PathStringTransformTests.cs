// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class PathStringTransformTests
    {
        // TODO: Theory
        [Fact]
        public void Set_Path_Success()
        {
            var transform = new PathStringTransform(PathStringTransform.TransformMode.Set, transformPathBase: false, "/value");
            var context = new RequestParametersTransformContext() { Path = "/foo" };
            transform.Apply(context);
            Assert.Equal("/value", context.Path);
        }
    }
}
