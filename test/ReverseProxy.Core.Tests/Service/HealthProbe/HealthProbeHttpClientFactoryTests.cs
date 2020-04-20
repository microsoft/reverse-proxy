// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.HealthProbe
{
    public class HealthProbeHttpClientFactoryTests
    {
        [Fact]
        public void HealthProbeHttpClientFactory_CreateHttpClient()
        {
            // Set up the factory.
            var factory = new HealthProbeHttpClientFactory();

            // Create http client.
            var httpClient1 = factory.CreateHttpClient();
            var httpClient2 = factory.CreateHttpClient();
            var httpClient3 = new HttpClient();

            // Validation
            Assert.NotNull(httpClient1);
            Assert.NotNull(httpClient2);
            Assert.NotSame(httpClient2, httpClient1);
            Assert.IsType<HttpClient>(httpClient1);
        }
    }
}
