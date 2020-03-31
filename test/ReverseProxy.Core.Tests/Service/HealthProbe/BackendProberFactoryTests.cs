// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using FluentAssertions;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Abstractions.Time;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.HealthProbe
{
    public class BackendProberFactoryTests : TestAutoMockBase
    {
        private readonly Mock<IMonotonicTimer> _timer;
        private readonly Mock<IOperationLogger> _operationLogger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHealthProbeHttpClientFactory _httpClientFactory;

        public BackendProberFactoryTests()
        {
            _timer = new Mock<IMonotonicTimer>();
            _loggerFactory = new LoggerFactory();
            _httpClientFactory = new HealthProbeHttpClientFactory();
            _operationLogger = new Mock<IOperationLogger>();
        }

        [Fact]
        public void WithNullParameter_BackendProberFactory_NotCreatebackendProber()
        {
            // Set up the factory.
            var factory = new BackendProberFactory(_timer.Object, _loggerFactory, _operationLogger.Object, _httpClientFactory);

            // Create prober should fail when parameter are set to null.
            Assert.Throws<ArgumentNullException>(() => factory.CreateBackendProber(null, null, null));
        }

        [Fact]
        public void BackendProberFactory_CreateBackendProber()
        {
            // Set up the factory.
            var factory = new BackendProberFactory(_timer.Object, _loggerFactory, _operationLogger.Object, _httpClientFactory);

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
            prober.Should().NotBeNull();
            prober.GetType().Should().Be(typeof(BackendProber));
        }
    }
}
