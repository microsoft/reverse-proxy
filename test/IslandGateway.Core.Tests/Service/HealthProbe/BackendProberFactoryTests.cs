// <copyright file="BackendProberFactoryTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

using FluentAssertions;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Abstractions.Time;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Management;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.HealthProbe
{
    public class BackendProberFactoryTests : TestAutoMockBase
    {
        private Mock<IMonotonicTimer> _timer;
        private Mock<IOperationLogger> _operationLogger;
        private ILoggerFactory _loggerFactory;
        private IHealthProbeHttpClientFactory _httpClientFactory;

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
            string backendId = "example";
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
