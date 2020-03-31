// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using FluentAssertions;
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
            httpClient1.Should().NotBeNull();
            httpClient2.Should().NotBeNull();
            httpClient1.Should().NotBeSameAs(httpClient2);
            httpClient1.GetType().Should().Be(typeof(HttpClient));
        }
    }
}
