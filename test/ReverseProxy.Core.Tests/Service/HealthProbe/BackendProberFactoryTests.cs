// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Abstractions.Time;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Management;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.HealthProbe
{
    public class BackendProberFactoryTests : TestAutoMockBase
    {
        private readonly Mock<IMonotonicTimer> _timer;
        private readonly Mock<IOperationLogger<BackendProber>> _operationLogger;
        private readonly ILogger<BackendProber> _logger;
        private readonly IHealthProbeHttpClientFactory _httpClientFactory;

        public BackendProberFactoryTests()
        {
            _timer = new Mock<IMonotonicTimer>();
            _logger = NullLogger<BackendProber>.Instance;
            _httpClientFactory = new HealthProbeHttpClientFactory();
            _operationLogger = new Mock<IOperationLogger<BackendProber>>();
        }

        [Fact]
        public void WithNullParameter_BackendProberFactory_NotCreatebackendProber()
        {
            // Set up the factory.
            var factory = new BackendProberFactory(_timer.Object, _logger, _operationLogger.Object, _httpClientFactory);

            // Create prober should fail when parameter are set to null.
            Assert.Throws<ArgumentNullException>(() => factory.CreateBackendProber(null, null, null));
        }

        [Fact]
        public void BackendProberFactory_CreateBackendProber()
        {
            // Set up the factory.
            var factory = new BackendProberFactory(_timer.Object, _logger, _operationLogger.Object, _httpClientFactory);

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
            var endpointManager = new EndpointManager();
            var prober = factory.CreateBackendProber(backendId, backendConfig, endpointManager);

            // Validate.
            Assert.NotNull(prober);
            Assert.IsType<BackendProber>(prober);
        }
    }
}
