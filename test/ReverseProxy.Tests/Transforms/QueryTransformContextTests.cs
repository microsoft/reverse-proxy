// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class QueryTransformContextTests
{
    [Fact]
    public void Collection_TryGetValue_CaseInsensitive()
    {
        var httpContext = new DefaultHttpContext { Request = { QueryString = new QueryString("?z=1") } };
        var queryTransformContext = new QueryTransformContext(httpContext.Request);
        queryTransformContext.Collection.TryGetValue("Z", out var result);
        Assert.Equal("1", result);
    }

    [Fact]
    public void Collection_RemoveKey_CaseInsensitive()
    {
        var httpContext = new DefaultHttpContext { Request = { QueryString = new QueryString("?z=1") } };
        var queryTransformContext = new QueryTransformContext(httpContext.Request);
        queryTransformContext.Collection.Remove("Z");
        Assert.False(queryTransformContext.QueryString.HasValue);
    }
}
