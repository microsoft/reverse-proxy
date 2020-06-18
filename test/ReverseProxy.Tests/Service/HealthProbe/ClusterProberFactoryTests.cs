// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    public class ClusterProberFactoryTests : TestAutoMockBase
    {
        private readonly Mock<IMonotonicTimer> _timer;
        private readonly Mock<IOperationLogger<ClusterProber>> _operationLogger;
        private readonly Mock<IRandomFactory> _randomFactory;
        private readonly ILogger<ClusterProber> _logger;
        private readonly IHealthProbeHttpClientFactory _httpClientFactory;

        public ClusterProberFactoryTests()
        {
            _timer = new Mock<IMonotonicTimer>();
            _logger = NullLogger<ClusterProber>.Instance;
            _httpClientFactory = new HealthProbeHttpClientFactory();
            _operationLogger = new Mock<IOperationLogger<ClusterProber>>();
            _randomFactory = new Mock<IRandomFactory>();
        }

        [Fact]
        public void WithNullParameter_ClusterProberFactory_NotCreateclusterProber()
        {
            // Set up the factory.
            var factory = new ClusterProberFactory(_timer.Object, _logger, _operationLogger.Object, _httpClientFactory, _randomFactory.Object);

            // Create prober should fail when parameter are set to null.
            Assert.Throws<ArgumentNullException>(() => factory.CreateClusterProber(null, null, null));
        }

        [Fact]
        public void ClusterProberFactory_CreateClusterProber()
        {
            // Set up the factory.
            var factory = new ClusterProberFactory(_timer.Object, _logger, _operationLogger.Object, _httpClientFactory, _randomFactory.Object);

            // Create probers.
            var clusterId = "example";
            var clusterConfig = new ClusterConfig(
                healthCheckOptions: new ClusterConfig.ClusterHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromMilliseconds(100),
                    timeout: TimeSpan.FromSeconds(60),
                    port: 8000,
                    path: "/example"),
                loadBalancingOptions: default,
                sessionAffinityOptions: default);
            var destinationManager = new DestinationManager();
            var prober = factory.CreateClusterProber(clusterId, clusterConfig, destinationManager);

            // Validate.
            Assert.NotNull(prober);
            Assert.IsType<ClusterProber>(prober);
        }
    }
}
