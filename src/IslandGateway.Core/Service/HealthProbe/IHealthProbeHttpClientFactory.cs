// <copyright file="IHealthProbeHttpClientFactory.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Net.Http;

namespace IslandGateway.Core.Service.HealthProbe
{
    /// <summary>
    /// Factory for creating http client instance. This factory let us able to inject http client into backendProber class.
    /// So that backendProber would be unit testable.
    /// </summary>
    internal interface IHealthProbeHttpClientFactory
    {
        /// <summary>
        /// Create a instance of http client.
        /// </summary>
        public HttpClient CreateHttpClient();
    }
}
