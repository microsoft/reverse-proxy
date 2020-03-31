// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Abstractions.Time;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Service.HealthProbe
{
    /// <summary>
    /// Factory class that provide instance of  <see cref="BackendProber"/> . The factory provide a way of dependency injection to pass
    /// prober into the healthProbeWorker class. Also make the healthProbeWorker unit testable.
    /// </summary>
    internal class BackendProberFactory : IBackendProberFactory
    {
        private readonly IMonotonicTimer _timer;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHealthProbeHttpClientFactory _httpClientFactory;
        private readonly IRandomFactory _randomFactory;
        private readonly IOperationLogger _operationLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendProberFactory"/> class.
        /// </summary>
        public BackendProberFactory(IMonotonicTimer timer, ILoggerFactory loggerFactory, IOperationLogger operationLogger, IHealthProbeHttpClientFactory httpClientFactory)
        {
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(loggerFactory, nameof(loggerFactory));
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(httpClientFactory, nameof(httpClientFactory));

            _timer = timer;
            _loggerFactory = loggerFactory;
            _httpClientFactory = httpClientFactory;
            _randomFactory = new RandomFactory();
            _operationLogger = operationLogger;
        }

        /// <inheritdoc/>
        public IBackendProber CreateBackendProber(string backendId, BackendConfig config, IEndpointManager endpointManager)
        {
            return new BackendProber(backendId, config, endpointManager, _timer, _loggerFactory.CreateLogger<BackendProber>(), _operationLogger, _httpClientFactory.CreateHttpClient(), _randomFactory);
        }
    }
}
