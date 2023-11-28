// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class DestinationPrefixTransformTests
{
    [Fact]
    public async Task UpdateDestinationPrefix()
    {
        const string newDestinationPrefix = "http://localhost:8080";
        var context = new RequestTransformContext()
        {
            DestinationPrefix = "http://contoso.com:5000"
        };
        var transform = new DestinationPrefixTransform(newDestinationPrefix);
        await transform.ApplyAsync(context);
    }

    private class DestinationPrefixTransform(string newDestinationPrefix) : RequestTransform
    {
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            context.DestinationPrefix = newDestinationPrefix;
            return ValueTask.CompletedTask;
        }
    }
}
