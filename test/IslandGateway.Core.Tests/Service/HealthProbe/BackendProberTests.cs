// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// #define LOCAL
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Telemetry;
using Microsoft.ReverseProxy.Common.Util;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.HealthProbe
{
    public class BackendProberTests : TestAutoMockBase
    {
    // TODO: a bandage for testing, can be removed easily once we fix the single task scheduler issue. We can still test it locally.
    #if LOCAL
        public const string SkipUnitTestSwitcher = null;
    #else
        public const string SkipUnitTestSwitcher = "Single threaded task scheduler is not running in single thread.Need to investigate the problem in branch: nulyu/last_puzzle";
    #endif

        private readonly string _backendId;
        private readonly BackendConfig _backendConfig;
        private readonly AsyncSemaphore _semaphore;
        private readonly HttpClient _badClient;
        private readonly HttpClient _goodClient;
        private readonly VirtualMonotonicTimer _timer;
        private readonly ILogger<BackendProber> _logger;
        private readonly IOperationLogger _operationLogger;

        private readonly Mock<IRandom> _fakeRandom;
        private readonly Mock<IRandomFactory> _randomFactory;

        public BackendProberTests()
        {
            // set up all the parameter needed for prober class
            _backendId = "example service";
            _backendConfig = new BackendConfig(
                healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromMilliseconds(100),
                    timeout: TimeSpan.FromSeconds(60),
                    port: 8000,
                    path: "/example"),
                loadBalancingOptions: default);
            _timer = new VirtualMonotonicTimer();
            _semaphore = new AsyncSemaphore(10);
            _fakeRandom = new Mock<IRandom>();
            _fakeRandom.Setup(p => p.Next(It.IsAny<int>())).Returns(0);
            _randomFactory = new Mock<IRandomFactory>();
            _randomFactory.Setup(f => f.CreateRandomInstance()).Returns(_fakeRandom.Object);

            // set up logger.
            var loggerFactory = new LoggerFactory();
            _logger = loggerFactory.CreateLogger<BackendProber>();
            _operationLogger = new TextOperationLogger(loggerFactory.CreateLogger<TextOperationLogger>());

            // set up the httpclient, we would mock the httpclient so we don not really make a real http request.
            _goodClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    return new HttpResponseMessage((HttpStatusCode)200);
                });

            _badClient = MockHttpHandler.CreateClient(
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
            var endpoints = EndpointManagerGenerator(1);
            var prober = new BackendProber(_backendId, _backendConfig, endpoints, _timer, _logger, _operationLogger, _goodClient, _randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'healthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            // Verify
            _fakeRandom.Verify(r => r.Next(It.IsAny<int>()));
        }

        [Fact(Skip = BackendProberTests.SkipUnitTestSwitcher)]
        public async Task WithHealthyBackend_ProbeEndpointAsync_Work()
        {
            // Set up endpoints to be probed. Let endpoints to be in healthy state.
            var endpoints = EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(_backendId, _backendConfig, endpoints, _timer, _logger, _operationLogger, _goodClient, _randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'healthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(101), cts.Token);
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
            var endpoints = EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(_backendId, _backendConfig, endpoints, _timer, _logger, _operationLogger, _badClient, _randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
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
            var endpoints = EndpointManagerGenerator(1);
            var prober = new BackendProber(_backendId, _backendConfig, endpoints, _timer, _logger, _operationLogger, _goodClient, _randomFactory.Object);

            // Stop the prober. If the unit test could complete, it demonstrates the probing process has been aborted.
            prober.Start(_semaphore);
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
            var endpoints = EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(_backendId, _backendConfig, endpoints, _timer, _logger, _operationLogger, httpErrorClient, _randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
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
            var endpoints = EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(_backendId, _backendConfig, endpoints, _timer, _logger, _operationLogger, httpTimeoutClient, _randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
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
            var endpoints = EndpointManagerGenerator(1);

            // Set up the prober.
            var prober = new BackendProber(_backendId, _backendConfig, endpoints, _timer, _logger, _operationLogger, httpTimeoutClient, _randomFactory.Object);

            // start probing the endpoint, it should mark our all our endpoints as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            endpoints.GetItems()[0].DynamicState.Value.Health.Should().NotBe(null);
            endpoints.GetItems()[0].DynamicState.Value.Health.Should().Be(EndpointHealth.Unhealthy);
        }

        private EndpointManager EndpointManagerGenerator(int num)
        {
            var endpointmanger = new EndpointManager();
            for (var i = 0; i < num; i++)
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
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _func;

            private MockHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
            {
                _func = func ?? throw new ArgumentNullException(nameof(func));
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
                return _func(request, cancellationToken);
            }
        }
    }
}
