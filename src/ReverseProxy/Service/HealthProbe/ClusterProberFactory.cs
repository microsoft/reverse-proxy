// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    /// <summary>
    /// Factory class that provide instance of  <see cref="ClusterProber"/> . The factory provide a way of dependency injection to pass
    /// prober into the healthProbeWorker class. Also make the healthProbeWorker unit testable.
    /// </summary>
    internal class ClusterProberFactory : IClusterProberFactory
    {
        private readonly IMonotonicTimer _timer;
        private readonly ILogger<ClusterProber> _logger;
        private readonly IHealthProbeHttpClientFactory _httpClientFactory;
        private readonly IRandomFactory _randomFactory;
        private readonly IOperationLogger<ClusterProber> _operationLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterProberFactory"/> class.
        /// </summary>
        public ClusterProberFactory(
            IMonotonicTimer timer,
            ILogger<ClusterProber> logger,
            IOperationLogger<ClusterProber> operationLogger,
            IHealthProbeHttpClientFactory httpClientFactory,
            IRandomFactory randomFactory)
        {
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(operationLogger));
        }

        /// <inheritdoc/>
        public IClusterProber CreateClusterProber(string clusterId, ClusterConfig config, IDestinationManager destinationManager)
        {
            return new ClusterProber(clusterId, config, destinationManager, _timer, _logger, _operationLogger, _httpClientFactory.CreateHttpClient(), _randomFactory);
        }
    }
}
