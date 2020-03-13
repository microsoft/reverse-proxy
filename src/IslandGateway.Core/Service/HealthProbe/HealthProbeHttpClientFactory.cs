// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace IslandGateway.Core.Service.HealthProbe
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
