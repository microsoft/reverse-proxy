// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    /// <summary>
    /// Factory for creating http client instance. This factory let us able to inject http client into clusterProber class.
    /// So that clusterProber would be unit testable.
    /// </summary>
    internal interface IHealthProbeHttpClientFactory
    {
        /// <summary>
        /// Create a instance of http client.
        /// </summary>
        public HttpClient CreateHttpClient();
    }
}
