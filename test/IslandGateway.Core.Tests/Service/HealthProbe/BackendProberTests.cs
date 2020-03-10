// <copyright file="BackendProberTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

// #define LOCAL
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Telemetry;
using IslandGateway.Common.Util;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Management;
using IslandGateway.CoreServicesBorrowed;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.HealthProbe
{
    public class BackendProberTests : TestAutoMockBase
    {
    // TODO: a bandage for testing, can be removed easily once we fix the single task scheduler issue. We can still test it locally.
    #if LOCAL
        public const string SkipUnitTestSwitcher = null;
    #else
        public const string SkipUnitTestSwitcher = "Single threaded task scheduler is not running in single thread.Need to investigate the problem in branch: nulyu/last_puzzle";
    #endif

        private string backendId;
        private BackendConfig backendConfig;
        private AsyncSemaphore semaphore;
        private HttpClient badClient;
        private HttpClient goodClient;
        private VirtualMonotonicTimer timer;
        private ILogger<BackendProber> logger;
        private IOperationLogger operationLogger;

        private Mock<IRandom> fakeRandom;
        private Mock<IRandomFactory> randomFactory;

        public BackendProberTests()
        {
            // set up all the parameter needed for prober class
            this.backendId = "example service";
            this.backendConfig = new BackendConfig(
                healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromMilliseconds(100),
                    timeout: TimeSpan.FromSeconds(60),
                    port: 8000,
                    path: "/example"),
                loadBalancingOptions: default);
            this.timer = new VirtualMonotonicTimer();
            this.semaphore = new AsyncSemaphore(10);
            this.fakeRandom = new Mock<IRandom>();
            this.fakeRandom.Setup(p => p.Next(It.IsAny<int>())).Returns(0);
            this.randomFactory = new Mock<IRandomFactory>();
            this.randomFactory.Setup(f => f.CreateRandomInstance()).Returns(this.fakeRandom.Object);

            // set up logger.
            var loggerFactory = new LoggerFactory();
            this.logger = loggerFactory.CreateLogger<BackendProber>();
            this.operationLogger = new TextOperationLogger(loggerFactory.CreateLogger<TextOperationLogger>());

            // set up the httpclient, we would mock the httpclient so we don not really make a real http request.
            this.goodClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    return new HttpResponseMessage((HttpStatusCode)200);
                });

            this.badClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    return new HttpResponseMessage((HttpStatusCode)404);
                });
        }

        [Fact(Skip = BackendProberTests.SkipUnitTestSwitcher)]
        public async Task ProbeEndpointAsync_Dithering_Work()
        {
            // Set up necessary parameter.
            var endpoints = this.EndpointManagerGenerator(1);
            var prober = new BackendProber(this.backendId, this.backendConfig, endpoints, this.timer, this.logger, this.operationLogger, this.goodClient, this.randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'healthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => this.timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(this.semaphore);
                    await this.timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            // Verify
            this.fakeRandom.Verify(r => r.Next(It.IsAny<int>()));
        }

        [Fact(Skip = BackendProberTests.SkipUnitTestSwitcher)]
        public async Task WithHealthyBackend_ProbeEndpointAsync_Work()
        {
            // Set up endpoints to be probed. Let endpoints to be in healthy state.
            var endpoints = this.EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(this.backendId, this.backendConfig, endpoints, this.timer, this.logger, this.operationLogger, this.goodClient, this.randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'healthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => this.timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(this.semaphore);
                    await this.timer.Delay(TimeSpan.FromMilliseconds(101), cts.Token);
                    await prober.StopAsync();
                });
            }

            endpoints.GetItems()[0].DynamicState.Value.Health.Should().NotBe(null);
            endpoints.GetItems()[0].DynamicState.Value.Health.Should().Be(EndpointHealth.Healthy);
        }

        [Fact(Skip = BackendProberTests.SkipUnitTestSwitcher)]
        public async Task WithUnhealthyBackend_ProbeEndpointAsync_Work()
        {
            // Set up endpoints to be probed.
            var endpoints = this.EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(this.backendId, this.backendConfig, endpoints, this.timer, this.logger, this.operationLogger, this.badClient, this.randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => this.timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(this.semaphore);
                    await this.timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            endpoints.GetItems()[0].DynamicState.Value.Health.Should().NotBe(null);
            endpoints.GetItems()[0].DynamicState.Value.Health.Should().Be(EndpointHealth.Unhealthy);
        }

        [Fact]
        public async Task StopAsync_Work()
        {
            // Set up necessary parameter.
            var endpoints = this.EndpointManagerGenerator(1);
            var prober = new BackendProber(this.backendId, this.backendConfig, endpoints, this.timer, this.logger, this.operationLogger, this.goodClient, this.randomFactory.Object);

            // Stop the prober. If the unit test could complete, it demonstrates the probing process has been aborted.
            prober.Start(this.semaphore);
            await prober.StopAsync();
        }

        [Fact(Skip = BackendProberTests.SkipUnitTestSwitcher)]
        public async Task ThroweHttpRequestException_ProbeEndpointsAsync_ShouldNotFail()
        {
            // Mock a exception that would happen during http request.
            var httpErrorClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    throw new HttpRequestException();
                });

            // Set up endpoints to be probed.
            var endpoints = this.EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(this.backendId, this.backendConfig, endpoints, this.timer, this.logger, this.operationLogger, httpErrorClient, this.randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => this.timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(this.semaphore);
                    await this.timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            endpoints.GetItems()[0].DynamicState.Value.Health.Should().NotBe(null);
            endpoints.GetItems()[0].DynamicState.Value.Health.Should().Be(EndpointHealth.Unhealthy);
        }

        [Fact(Skip = BackendProberTests.SkipUnitTestSwitcher)]
        public async Task ThroweTimeoutException_ProbeEndpointsAsync_ShouldNotFail()
        {
            // Mock a exception that would happen during http request.
            var httpTimeoutClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    throw new OperationCanceledException();
                });

            // Set up endpoints to be probed.
            var endpoints = this.EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(this.backendId, this.backendConfig, endpoints, this.timer, this.logger, this.operationLogger, httpTimeoutClient, this.randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => this.timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(this.semaphore);
                    await this.timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            endpoints.GetItems()[0].DynamicState.Value.Health.Should().NotBe(null);
            endpoints.GetItems()[0].DynamicState.Value.Health.Should().Be(EndpointHealth.Unhealthy);
        }

        [Fact(Skip = BackendProberTests.SkipUnitTestSwitcher)]
        public async Task ThroweUnexpectedException_ProbeEndpointsAsync_ShouldThrowButNotFail()
        {
            // Mock a exception that would happen during http request.
            var httpTimeoutClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    throw new Exception();
                });

            // Set up endpoints to be probed.
            var endpoints = this.EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(this.backendId, this.backendConfig, endpoints, this.timer, this.logger, this.operationLogger, httpTimeoutClient, this.randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => this.timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(this.semaphore);
                    await this.timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            endpoints.GetItems()[0].DynamicState.Value.Health.Should().NotBe(null);
            endpoints.GetItems()[0].DynamicState.Value.Health.Should().Be(EndpointHealth.Unhealthy);
        }

        private EndpointManager EndpointManagerGenerator(int num)
        {
            var endpointmanger = new EndpointManager();
            for (int i = 0; i < num; i++)
            {
                endpointmanger.GetOrCreateItem("endpoint" + i.ToString(), item => { item.Config.Value = new EndpointConfig("https://localhost:123/a/b/api/test"); });
            }

            return endpointmanger;
        }

        /// <summary>
        /// Spawns a set of task to schedule concurrently, then joins on their completion.
        /// </summary>
        private Task RunConcurrently(params Func<Task>[] operations)
        {
            return Task.WhenAll(operations.Select(operation => TaskScheduler.Current.Run(operation)));
        }

        private class MockHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func;

            private MockHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
            {
                this.func = func ?? throw new ArgumentNullException(nameof(func));
            }

            public static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
            {
                var handler = new MockHttpHandler(func);
                return new HttpClient(handler)
                {
                    Timeout = Timeout.InfiniteTimeSpan,
                };
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return this.func(request, cancellationToken);
            }
        }
    }
}
