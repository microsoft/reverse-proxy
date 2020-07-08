// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    internal class ClusterProber : IClusterProber
    {
        private static readonly TimeSpan _httpTimeoutInterval = TimeSpan.FromSeconds(60);

        // TODO: Replace with thread-safe and unit-testable random provider.
        private static readonly int _ditheringIntervalInMilliseconds = 1000;
        private readonly IRandomFactory _randomFactory;
        private readonly IDestinationManager _destinationManager;
        private readonly ILogger<ClusterProber> _logger;
        private readonly IOperationLogger<ClusterProber> _operationLogger;

        private Task _backgroundPollingLoopTask;
        private readonly TimeSpan _healthCheckInterval;
        private readonly Uri _healthControllerUrl;
        private readonly IMonotonicTimer _timer;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly HttpClient _clusterProbeHttpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterProber"/> class.
        /// HealthProber is the unit that checks the health state for all destinations(replica) of a cluster(service)
        /// HealthProbe would query the health controller of the destination and update the health state for every destination
        /// periodically base on the time interval user specified in cluster.
        /// </summary>
        public ClusterProber(string clusterId, ClusterConfig config, IDestinationManager destinationManager, IMonotonicTimer timer, ILogger<ClusterProber> logger, IOperationLogger<ClusterProber> operationLogger, HttpClient httpClient, IRandomFactory randomFactory)
        {
            ClusterId = clusterId ?? throw new ArgumentNullException(nameof(clusterId));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _destinationManager = destinationManager ?? throw new ArgumentNullException(nameof(destinationManager));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(operationLogger));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
            _clusterProbeHttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _healthControllerUrl = new Uri(Config.HealthCheckOptions.Path, UriKind.Relative);
            _healthCheckInterval = Config.HealthCheckOptions.Interval;

        }

        /// <inheritdoc/>
        public string ClusterId { get; }

        /// <inheritdoc/>
        public ClusterConfig Config { get; }

        /// <inheritdoc/>
        public void Start(SemaphoreSlim semaphore)
        {
            // Start background work, wake up for every interval(set in cluster config).
            _backgroundPollingLoopTask = Task.Run(() => ProbeDestinationsAsync(semaphore, _cts.Token));
        }

        /// <inheritdoc/>
        public async Task StopAsync()
        {
            // Generate Cancellation token.
            _cts.Cancel();

            // Wait until this prober is done, gracefully shut down the probing process.
            await _backgroundPollingLoopTask;

            // Release the cancellation source
            _cts.Dispose();
            Log.ProberStopped(_logger, ClusterId);
        }

        /// <summary>
        /// Async methods that conduct http request to query the health state from the health controller of every destination.
        /// </summary>
        private async Task ProbeDestinationsAsync(SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            // Always continue probing until receives cancellation token.
            try
            {
                while (true)
                {
                    var probeTasks = new List<Task>();
                    cancellationToken.ThrowIfCancellationRequested();

                    // try catch to prevent while loop crashed by any nonfatal exception
                    try
                    {
                        // Submit probe requests.
                        foreach (var destination in _destinationManager.GetItems())
                        {
                            // Start a single probing attempt.
                            probeTasks.Add(ProbeDestinationAsync(destination, semaphore, cancellationToken));
                        }

                        await Task.WhenAll(probeTasks);
                    }
                    catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                    {
                        // If the cancel requested by our StopAsync method. It is a expected graceful shut down.
                        throw;
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        // Swallow the nonfatal exception, we don not want the health check to break.
                        Log.ProberFailed(_logger, ClusterId, ex);
                    }

                    Log.ProberChecked(_logger, ClusterId, _healthCheckInterval.TotalSeconds);

                    // Wait for next probe cycle.
                    await _timer.Delay(_healthCheckInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // If the cancel requested by our StopAsync method. It is a expected graceful shut down.
                Log.ProberGracefulShutdown(_logger, ClusterId);
            }
            catch (Exception ex)
            {
                // Swallow the exception, we want the health check continuously running like the heartbeat.
                Log.ProberFailed(_logger, ClusterId, ex);
            }
        }

        /// <summary>
        /// A probe attempt to one destination.
        /// </summary>
        private async Task ProbeDestinationAsync(DestinationInfo destination, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            // Conduct a dither for every endpoint probe to optimize concurrency.
            var randomDither = _randomFactory.CreateRandomInstance();
            await _timer.Delay(TimeSpan.FromMilliseconds(randomDither.Next(_ditheringIntervalInMilliseconds)), cancellationToken);

            var outcome = HealthProbeOutcome.Unknown;
            string logDetail = null;

            // Enforce max concurrency.
            await semaphore.WaitAsync();
            Log.ProberStarted(_logger, ClusterId, destination.DestinationId);
            try
            {
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                {
                    // Set up timeout and start probing.
                    timeoutCts.CancelAfter(_httpTimeoutInterval, _timer);
                    var response = await _operationLogger.ExecuteAsync(
                            "ReverseProxy.Service.HealthProbe",
                            () => _clusterProbeHttpClient.GetAsync(new Uri(new Uri(destination.Config.Address, UriKind.Absolute), _healthControllerUrl), timeoutCts.Token));

                    // Collect response status.
                    outcome = response.IsSuccessStatusCode ? HealthProbeOutcome.Success : HealthProbeOutcome.HttpFailure;
                    logDetail = $"Received status code {(int)response.StatusCode}";
                }
            }
            catch (HttpRequestException ex)
            {
                // If there is a error during the http request process. Swallow the error and log error message.
                outcome = HealthProbeOutcome.TransportFailure;
                logDetail = ex.Message;
            }
            catch (OperationCanceledException)
            {
                // If the cancel requested by our StopAsync method. It is a expected graceful shut down.
                if (_cts.IsCancellationRequested)
                {
                    outcome = HealthProbeOutcome.Canceled;
                    logDetail = "Operation deliberately canceled";
                    throw;
                }
                else
                {
                    outcome = HealthProbeOutcome.Timeout;
                    logDetail = $"Health probe timed out after {Config.HealthCheckOptions.Interval.TotalSeconds} second";
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Prober for '{destination.DestinationId}' encounters unexpected exception.", ex);
            }
            finally
            {
                if (outcome != HealthProbeOutcome.Canceled)
                {
                    // Update the health state base on the response.
                    var healthState = outcome == HealthProbeOutcome.Success ? DestinationHealth.Healthy : DestinationHealth.Unhealthy;
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(healthState);
                    Log.ProberResult(_logger, destination.DestinationId, outcome, logDetail);
                }

                // The probe operation is done, release the semaphore to allow other probes to proceed.
                semaphore.Release();
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _proberStopped = LoggerMessage.Define<string>(
               LogLevel.Information,
               EventIds.ProberStopped,
               "The cluster prober for '{clusterId}' has stopped.");

            private static readonly Action<ILogger, string, Exception> _proberFailed = LoggerMessage.Define<string>(
               LogLevel.Error,
               EventIds.ProberFailed,
               "Prober for '{clusterId}' encounters unexpected exception.");

            private static readonly Action<ILogger, string, double, Exception> _proberChecked = LoggerMessage.Define<string, double>(
               LogLevel.Information,
               EventIds.ProberChecked,
               "The cluster prober for '{clusterId}' has checked all destinations with time interval {proberCheckInterval} second.");

            private static readonly Action<ILogger, string, Exception> _proberGracefulShutdown = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.ProberGracefulShutdown,
                "Prober for cluster '{clusterId}' has gracefully shutdown.");

            private static readonly Action<ILogger, string, string, Exception> _proberStarted = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.ProberStarted,
                "The cluster prober for cluster: '{clusterId}', endpoint: `{endpointId}` has started.");

            private static readonly Action<ILogger, string, HealthProbeOutcome, string, Exception> _proberResult = LoggerMessage.Define<string, HealthProbeOutcome, string>(
                LogLevel.Information,
                EventIds.ProberResult,
                "Health probe result for endpoint '{endpointId}': {probeOutcome}. Details: {probeOutcomeDetail}");

            public static void ProberStopped(ILogger logger, string clusterId)
            {
                _proberStopped(logger, clusterId, null);
            }

            public static void ProberFailed(ILogger logger, string clusterId, Exception exception)
            {
                _proberFailed(logger, clusterId, exception);
            }

            public static void ProberChecked(ILogger logger, string clusterId, double proberCheckInterval)
            {
                _proberChecked(logger, clusterId, proberCheckInterval, null);
            }

            public static void ProberGracefulShutdown(ILogger logger, string clusterId)
            {
                _proberGracefulShutdown(logger, clusterId, null);
            }

            public static void ProberStarted(ILogger logger, string clusterId, string endpointId)
            {
                _proberStarted(logger, clusterId, endpointId, null);
            }

            public static void ProberResult(ILogger logger, string endpointId, HealthProbeOutcome proberOutcome, string probeOutcomeDetail)
            {
                _proberResult(logger, endpointId, proberOutcome, probeOutcomeDetail, null);
            }
        }
    }
}
