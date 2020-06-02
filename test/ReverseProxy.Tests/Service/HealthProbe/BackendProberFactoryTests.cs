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
    public class BackendProberFactoryTests : TestAutoMockBase
    {
        private readonly Mock<IMonotonicTimer> _timer;
        private readonly Mock<IOperationLogger<BackendProber>> _operationLogger;
        private readonly Mock<IRandomFactory> _randomFactory;
        private readonly ILogger<BackendProber> _logger;
        private readonly IHealthProbeHttpClientFactory _httpClientFactory;

        public BackendProberFactoryTests()
        {
            _timer = new Mock<IMonotonicTimer>();
            _logger = NullLogger<BackendProber>.Instance;
            _httpClientFactory = new HealthProbeHttpClientFactory();
            _operationLogger = new Mock<IOperationLogger<BackendProber>>();
            _randomFactory = new Mock<IRandomFactory>();
        }

        [Fact]
        public void WithNullParameter_BackendProberFactory_NotCreatebackendProber()
        {
            // Set up the factory.
            var factory = new BackendProberFactory(_timer.Object, _logger, _operationLogger.Object, _httpClientFactory, _randomFactory.Object);

            // Create prober should fail when parameter are set to null.
            Assert.Throws<ArgumentNullException>(() => factory.CreateBackendProber(null, null, null));
        }

        [Fact]
        public void BackendProberFactory_CreateBackendProber()
        {
            // Set up the factory.
            var factory = new BackendProberFactory(_timer.Object, _logger, _operationLogger.Object, _httpClientFactory, _randomFactory.Object);

            // Create probers.
            var backendId = "example";
            var backendConfig = new BackendConfig(
                healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromMilliseconds(100),
                    timeout: TimeSpan.FromSeconds(60),
                    port: 8000,
                    path: "/example"),
                loadBalancingOptions: default);
            var destinationManager = new DestinationManager();
            var prober = factory.CreateBackendProber(backendId, backendConfig, destinationManager);

            // Validate.
            Assert.NotNull(prober);
            Assert.IsType<BackendProber>(prober);
        }
    }
}
