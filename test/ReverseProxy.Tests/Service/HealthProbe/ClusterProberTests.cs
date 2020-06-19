// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// #define LOCAL
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Telemetry;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    public class ClusterProberTests : TestAutoMockBase
    {
    // TODO: a bandage for testing, can be removed easily once we fix the single task scheduler issue. We can still test it locally.
    #if LOCAL
        public const string SkipUnitTestSwitcher = null;
    #else
        public const string SkipUnitTestSwitcher = "Single threaded task scheduler is not running in single thread.Need to investigate the problem in branch: nulyu/last_puzzle";
    #endif

        private readonly string _clusterId;
        private readonly ClusterConfig _clusterConfig;
        private readonly SemaphoreSlim _semaphore;
        private readonly HttpClient _badClient;
        private readonly HttpClient _goodClient;
        private readonly VirtualMonotonicTimer _timer;
        private readonly ILogger<ClusterProber> _logger;
        private readonly IOperationLogger<ClusterProber> _operationLogger;

        private readonly Mock<Random> _fakeRandom;
        private readonly Mock<IRandomFactory> _randomFactory;

        public ClusterProberTests()
        {
            // set up all the parameter needed for prober class
            _clusterId = "example service";
            _clusterConfig = new ClusterConfig(
                healthCheckOptions: new ClusterConfig.ClusterHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromMilliseconds(100),
                    timeout: TimeSpan.FromSeconds(60),
                    port: 8000,
                    path: "/example"),
                loadBalancingOptions: default,
                sessionAffinityOptions: default);
            _timer = new VirtualMonotonicTimer();
            _semaphore = new SemaphoreSlim(10);
            _fakeRandom = new Mock<Random>();
            _fakeRandom.Setup(p => p.Next(It.IsAny<int>())).Returns(0);
            _randomFactory = new Mock<IRandomFactory>();
            _randomFactory.Setup(f => f.CreateRandomInstance()).Returns(_fakeRandom.Object);

            // set up logger.
            var loggerFactory = new LoggerFactory();
            _logger = loggerFactory.CreateLogger<ClusterProber>();
            _operationLogger = new TextOperationLogger<ClusterProber>(loggerFactory.CreateLogger<ClusterProber>());

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

        [Fact(Skip = ClusterProberTests.SkipUnitTestSwitcher)]
        public async Task ProbeDestinationAsync_Dithering_Work()
        {
            // Set up necessary parameter.
            var destinations = DestinationManagerGenerator(1);
            var prober = new ClusterProber(_clusterId, _clusterConfig, destinations, _timer, _logger, _operationLogger, _goodClient, _randomFactory.Object);

            // start probing the destination, it should mark our all our destinations as state 'healthy'.
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

        [Fact(Skip = ClusterProberTests.SkipUnitTestSwitcher)]
        public async Task WithHealthyCluster_ProbeDestinationAsync_Work()
        {
            // Set up destinations to be probed. Let destinations to be in healthy state.
            var destinations = DestinationManagerGenerator(1);

            // Set up the prober.
            var prober = new ClusterProber(_clusterId, _clusterConfig, destinations, _timer, _logger, _operationLogger, _goodClient, _randomFactory.Object);

            // start probing the destination, it should mark our all our destinations as state 'healthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(101), cts.Token);
                    await prober.StopAsync();
                });
            }

            Assert.Equal(DestinationHealth.Healthy, destinations.GetItems()[0].DynamicState.Value.Health);
        }

        [Fact(Skip = ClusterProberTests.SkipUnitTestSwitcher)]
        public async Task WithUnhealthyCluster_ProbeDestinationAsync_Work()
        {
            // Set up destinations to be probed.
            var destinations = DestinationManagerGenerator(1);

            // Set up the prober.
            var prober = new ClusterProber(_clusterId, _clusterConfig, destinations, _timer, _logger, _operationLogger, _badClient, _randomFactory.Object);

            // start probing the destination, it should mark our all our destinations as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            Assert.Equal(DestinationHealth.Unhealthy, destinations.GetItems()[0].DynamicState.Value.Health);
        }

        [Fact]
        public async Task StopAsync_Work()
        {
            // Set up necessary parameter.
            var destinations = DestinationManagerGenerator(1);
            var prober = new ClusterProber(_clusterId, _clusterConfig, destinations, _timer, _logger, _operationLogger, _goodClient, _randomFactory.Object);

            // Stop the prober. If the unit test could complete, it demonstrates the probing process has been aborted.
            prober.Start(_semaphore);
            await prober.StopAsync();
        }

        [Fact(Skip = ClusterProberTests.SkipUnitTestSwitcher)]
        public async Task ThroweHttpRequestException_ProbeDestinationsAsync_ShouldNotFail()
        {
            // Mock a exception that would happen during http request.
            var httpErrorClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    throw new HttpRequestException();
                });

            // Set up destinations to be probed.
            var destinations = DestinationManagerGenerator(1);

            // Set up the prober.
            var prober = new ClusterProber(_clusterId, _clusterConfig, destinations, _timer, _logger, _operationLogger, httpErrorClient, _randomFactory.Object);

            // start probing the destination, it should mark our all our destinations as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            Assert.Equal(DestinationHealth.Unhealthy, destinations.GetItems()[0].DynamicState.Value.Health);
        }

        [Fact(Skip = ClusterProberTests.SkipUnitTestSwitcher)]
        public async Task ThroweTimeoutException_ProbeDestinationsAsync_ShouldNotFail()
        {
            // Mock a exception that would happen during http request.
            var httpTimeoutClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    throw new OperationCanceledException();
                });

            // Set up destinations to be probed.
            var destinations = DestinationManagerGenerator(1);

            // Set up the prober.
            var prober = new ClusterProber(_clusterId, _clusterConfig, destinations, _timer, _logger, _operationLogger, httpTimeoutClient, _randomFactory.Object);

            // start probing the destination, it should mark our all our destinations as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            Assert.Equal(DestinationHealth.Unhealthy, destinations.GetItems()[0].DynamicState.Value.Health);
        }

        [Fact(Skip = ClusterProberTests.SkipUnitTestSwitcher)]
        public async Task ThroweUnexpectedException_ProbeDestinationsAsync_ShouldThrowButNotFail()
        {
            // Mock a exception that would happen during http request.
            var httpTimeoutClient = MockHttpHandler.CreateClient(
                async (HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    await Task.Yield();
                    throw new Exception();
                });

            // Set up destinations to be probed.
            var destinations = DestinationManagerGenerator(1);

            // Set up the prober.
            var prober = new ClusterProber(_clusterId, _clusterConfig, destinations, _timer, _logger, _operationLogger, httpTimeoutClient, _randomFactory.Object);

            // start probing the destination, it should mark our all our destinations as state 'unhealthy'.
            using (var cts = new CancellationTokenSource())
            {
                await new SingleThreadedTaskScheduler() { OnIdle = () => _timer.AdvanceStep() }.Run(async () =>
                {
                    prober.Start(_semaphore);
                    await _timer.Delay(TimeSpan.FromMilliseconds(501), cts.Token);
                    await prober.StopAsync();
                });
            }

            Assert.Equal(DestinationHealth.Unhealthy, destinations.GetItems()[0].DynamicState.Value.Health);
        }

        private DestinationManager DestinationManagerGenerator(int num)
        {
            var destinationManger = new DestinationManager();
            for (var i = 0; i < num; i++)
            {
                destinationManger.GetOrCreateItem("destination" + i.ToString(), item => { item.Config.Value = new DestinationConfig("https://localhost:123/a/b/api/test"); });
            }

            return destinationManger;
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
