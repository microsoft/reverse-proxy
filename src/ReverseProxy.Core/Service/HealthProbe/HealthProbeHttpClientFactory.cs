// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.ReverseProxy.Core.Service.HealthProbe
{
    internal class HealthProbeHttpClientFactory : IHealthProbeHttpClientFactory
    {
        /// <inheritdoc/>
        public HttpClient CreateHttpClient()
        {
            // TODO: Do something similar to ProxyHttpClientFactory
            return new HttpClient();
        }
    }
}
