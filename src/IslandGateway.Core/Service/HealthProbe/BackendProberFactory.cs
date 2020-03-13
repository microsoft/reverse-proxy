// <copyright file="BackendProberFactory.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Abstractions.Time;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Management;
using IslandGateway.Utilities;
using Microsoft.Extensions.Logging;

namespace IslandGateway.Core.Service.HealthProbe
{
    /// <summary>
    /// Factory class that provide instance of  <see cref="BackendProber"/> . The factory provide a way of dependency injection to pass
    /// prober into the healthProbeWorker class. Also make the healthProbeWorker unit testable.
    /// </summary>
    internal class BackendProberFactory : IBackendProberFactory
    {
        private IMonotonicTimer _timer;
        private ILoggerFactory _loggerFactory;
        private IHealthProbeHttpClientFactory _httpClientFactory;
        private IRandomFactory _randomFactory;
        private IOperationLogger _operationLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendProberFactory"/> class.
        /// </summary>
        public BackendProberFactory(IMonotonicTimer timer, ILoggerFactory loggerFactory, IOperationLogger operationLogger, IHealthProbeHttpClientFactory httpClientFactory)
        {
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(loggerFactory, nameof(loggerFactory));
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(httpClientFactory, nameof(httpClientFactory));

            this._timer = timer;
            this._loggerFactory = loggerFactory;
            this._httpClientFactory = httpClientFactory;
            this._randomFactory = new RandomFactory();
            this._operationLogger = operationLogger;
        }

        /// <inheritdoc/>
        public IBackendProber CreateBackendProber(string backendId, BackendConfig config, IEndpointManager endpointManager)
        {
            return new BackendProber(backendId, config, endpointManager, this._timer, this._loggerFactory.CreateLogger<BackendProber>(), this._operationLogger, this._httpClientFactory.CreateHttpClient(), this._randomFactory);
        }
    }
}
