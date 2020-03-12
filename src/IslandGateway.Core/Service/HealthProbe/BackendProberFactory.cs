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
        private IMonotonicTimer timer;
        private ILoggerFactory loggerFactory;
        private IHealthProbeHttpClientFactory httpClientFactory;
        private IRandomFactory randomFactory;
        private IOperationLogger operationLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendProberFactory"/> class.
        /// </summary>
        public BackendProberFactory(IMonotonicTimer timer, ILoggerFactory loggerFactory, IOperationLogger operationLogger, IHealthProbeHttpClientFactory httpClientFactory)
        {
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(loggerFactory, nameof(loggerFactory));
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(httpClientFactory, nameof(httpClientFactory));

            this.timer = timer;
            this.loggerFactory = loggerFactory;
            this.httpClientFactory = httpClientFactory;
            this.randomFactory = new RandomFactory();
            this.operationLogger = operationLogger;
        }

        /// <inheritdoc/>
        public IBackendProber CreateBackendProber(string backendId, BackendConfig config, IEndpointManager endpointManager)
        {
            return new BackendProber(backendId, config, endpointManager, this.timer, this.loggerFactory.CreateLogger<BackendProber>(), this.operationLogger, this.httpClientFactory.CreateHttpClient(), this.randomFactory);
        }
    }
}
