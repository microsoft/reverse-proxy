// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class HttpMethodTransformTests
    {
        [Theory]
        [InlineData("PUT", "POST", "PUT", "POST")]
        [InlineData("PUT", "POST", "POST", "POST")]
        [InlineData("PUT", "POST", "GET", "GET")]
        public async Task HttpMethod_Works(string fromMethod, string toMethod, string requestMethod, string expected)
        {
            var httpContext = new DefaultHttpContext();
            var request = new HttpRequestMessage() { Method = new HttpMethod(requestMethod) };
            var context = new RequestTransformContext()
            {
                HttpContext = httpContext,
                ProxyRequest = request,
            };
            var transform = new HttpMethodTransform(fromMethod, toMethod);
            await transform.ApplyAsync(context);
            Assert.Equal(expected, request.Method.Method);
        }
    }
}
