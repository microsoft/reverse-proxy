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
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    internal class BackendProber : IBackendProber
    {
        private static readonly TimeSpan _httpTimeoutInterval = TimeSpan.FromSeconds(60);

        // TODO: Replace with thread-safe and unit-testable random provider.
        private static readonly int _ditheringIntervalInMilliseconds = 1000;
        private readonly IRandomFactory _randomFactory;
        private readonly IDestinationManager _destinationManager;
        private readonly ILogger<BackendProber> _logger;
        private readonly IOperationLogger<BackendProber> _operationLogger;

        private Task _backgroundPollingLoopTask;
        private readonly TimeSpan _healthCheckInterval;
        private readonly Uri _healthControllerUrl;
        private readonly IMonotonicTimer _timer;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly HttpClient _backendProbeHttpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendProber"/> class.
        /// HealthProber is the unit that checks the health state for all destinations(replica) of a backend(service)
        /// HealthProbe would query the health controller of the destination and update the health state for every destination
        /// periodically base on the time interval user specified in backend.
        /// </summary>
        public BackendProber(string backendId, BackendConfig config, IDestinationManager destinationManager, IMonotonicTimer timer, ILogger<BackendProber> logger, IOperationLogger<BackendProber> operationLogger, HttpClient httpClient, IRandomFactory randomFactory)
        {
            Contracts.CheckValue(backendId, nameof(backendId));
            Contracts.CheckValue(config, nameof(config));
            Contracts.CheckValue(destinationManager, nameof(destinationManager));
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(httpClient, nameof(httpClient));
            Contracts.CheckValue(randomFactory, nameof(randomFactory));

            BackendId = backendId;
            Config = config;
            _destinationManager = destinationManager;
            _timer = timer;
            _logger = logger;
            _operationLogger = operationLogger;
            _randomFactory = randomFactory;

            _healthControllerUrl = new Uri(Config.HealthCheckOptions.Path, UriKind.Relative);
            _healthCheckInterval = Config.HealthCheckOptions.Interval;

            _backendProbeHttpClient = httpClient;
        }

        /// <inheritdoc/>
        public string BackendId { get; }

        /// <inheritdoc/>
        public BackendConfig Config { get; }

        /// <inheritdoc/>
        public void Start(AsyncSemaphore semaphore)
        {
            // Start background work, wake up for every interval(set in backend config).
            _backgroundPollingLoopTask = TaskScheduler.Current.Run(() => ProbeDestinationsAsync(semaphore, _cts.Token));
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
            Log.ProberStopped(_logger, BackendId);
        }

        /// <summary>
        /// Async methods that conduct http request to query the health state from the health controller of every destination.
        /// </summary>
        private async Task ProbeDestinationsAsync(AsyncSemaphore semaphore, CancellationToken cancellationToken)
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
                        Log.ProberFailed(_logger, BackendId, ex);
                    }

                    Log.ProberChecked(_logger, BackendId, _healthCheckInterval.TotalSeconds);

                    // Wait for next probe cycle.
                    await _timer.Delay(_healthCheckInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // If the cancel requested by our StopAsync method. It is a expected graceful shut down.
                Log.ProberGracefulShutdown(_logger, BackendId);
            }
            catch (Exception ex)
            {
                // Swallow the exception, we want the health check continuously running like the heartbeat.
                Log.ProberFailed(_logger, BackendId, ex);
            }
        }

        /// <summary>
        /// A probe attempt to one destination.
        /// </summary>
        private async Task ProbeDestinationAsync(DestinationInfo destination, AsyncSemaphore semaphore, CancellationToken cancellationToken)
        {
            // Conduct a dither for every endpoint probe to optimize concurrency.
            var randomDither = _randomFactory.CreateRandomInstance();
            await _timer.Delay(TimeSpan.FromMilliseconds(randomDither.Next(_ditheringIntervalInMilliseconds)), cancellationToken);

            var outcome = HealthProbeOutcome.Unknown;
            string logDetail = null;

            // Enforce max concurrency.
            await semaphore.WaitAsync();
            Log.ProberStarted(_logger, BackendId, destination.DestinationId);
            try
            {
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                {
                    // Set up timeout and start probing.
                    timeoutCts.CancelAfter(_httpTimeoutInterval, _timer);
                    var response = await _operationLogger.ExecuteAsync(
                            "ReverseProxy.Service.HealthProbe",
                            () => _backendProbeHttpClient.GetAsync(new Uri(new Uri(destination.Config.Value.Address, UriKind.Absolute), _healthControllerUrl), timeoutCts.Token));

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
                    destination.DynamicState.Value = new DestinationDynamicState(healthState);
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
               "The backend prober for '{backendId}' has stopped.");

            private static readonly Action<ILogger, string, Exception> _proberFailed = LoggerMessage.Define<string>(
               LogLevel.Error,
               EventIds.ProberFailed,
               "Prober for '{backendId}' encounters unexpected exception.");

            private static readonly Action<ILogger, string, double, Exception> _proberChecked = LoggerMessage.Define<string, double>(
               LogLevel.Information,
               EventIds.ProberChecked,
               "The backend prober for '{backendId}' has checked all destinations with time interval {proberCheckInterval} second.");

            private static readonly Action<ILogger, string, Exception> _proberGracefulShutdown = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.ProberGracefulShutdown,
                "Prober for backend '{backendId}' has gracefully shutdown.");

            private static readonly Action<ILogger, string, string, Exception> _proberStarted = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.ProberStarted,
                "The backend prober for backend: '{backendId}', endpoint: `{endpointId}` has started.");

            private static readonly Action<ILogger, string, HealthProbeOutcome, string, Exception> _proberResult = LoggerMessage.Define<string, HealthProbeOutcome, string>(
                LogLevel.Information,
                EventIds.ProberResult,
                "Health probe result for endpoint '{endpointId}': {probeOutcome}. Details: {probeOutcomeDetail}");

            public static void ProberStopped(ILogger logger, string backendId)
            {
                _proberStopped(logger, backendId, null);
            }

            public static void ProberFailed(ILogger logger, string backendId, Exception exception)
            {
                _proberFailed(logger, backendId, exception);
            }

            public static void ProberChecked(ILogger logger, string backendId, double proberCheckInterval)
            {
                _proberChecked(logger, backendId, proberCheckInterval, null);
            }

            public static void ProberGracefulShutdown(ILogger logger, string backendId)
            {
                _proberGracefulShutdown(logger, backendId, null);
            }

            public static void ProberStarted(ILogger logger, string backendId, string endpointId)
            {
                _proberStarted(logger, backendId, endpointId, null);
            }

            public static void ProberResult(ILogger logger, string endpointId, HealthProbeOutcome proberOutcome, string probeOutcomeDetail)
            {
                _proberResult(logger, endpointId, proberOutcome, probeOutcomeDetail, null);
            }
        }
    }
}
