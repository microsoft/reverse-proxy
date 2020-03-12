// <copyright file="BackendProber.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Abstractions.Time;
using IslandGateway.Common.Util;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Management;
using IslandGateway.Utilities;
using Microsoft.Extensions.Logging;

namespace IslandGateway.Core.Service.HealthProbe
{
    internal class BackendProber : IBackendProber
    {
        private static TimeSpan httpTimeoutInterval = TimeSpan.FromSeconds(60);

        // TODO: Replace with thread-safe and unit-testable random provider.
        private static int ditheringIntervalInMilliseconds = 1000;
        private IRandomFactory randomFactory;
        private IEndpointManager endpointManager;
        private ILogger<BackendProber> logger;
        private IOperationLogger operationLogger;

        private Task backgroundPollingLoopTask;
        private TimeSpan healthCheckInterval;
        private Uri healthControllerUrl;
        private IMonotonicTimer timer;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private HttpClient backendProbeHttpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendProber"/> class.
        /// HealthProber is the unit that checks the health state for all endpoints(replica) of a backend(service)
        /// HealthProbe would query the health controller of the endpoint and update the health state for every endpoint
        /// periodically base on the time interval user specified in backend.
        /// </summary>
        public BackendProber(string backendId, BackendConfig config, IEndpointManager endpointManager, IMonotonicTimer timer, ILogger<BackendProber> logger, IOperationLogger operationLogger, HttpClient httpClient, IRandomFactory randomFactory)
        {
            Contracts.CheckValue(backendId, nameof(backendId));
            Contracts.CheckValue(config, nameof(config));
            Contracts.CheckValue(endpointManager, nameof(endpointManager));
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(httpClient, nameof(httpClient));
            Contracts.CheckValue(randomFactory, nameof(randomFactory));

            this.BackendId = backendId;
            this.Config = config;
            this.endpointManager = endpointManager;
            this.timer = timer;
            this.logger = logger;
            this.operationLogger = operationLogger;
            this.randomFactory = randomFactory;

            this.healthControllerUrl = new Uri(this.Config.HealthCheckOptions.Path, UriKind.Relative);
            this.healthCheckInterval = this.Config.HealthCheckOptions.Interval;

            this.backendProbeHttpClient = httpClient;
        }

        /// <inheritdoc/>
        public string BackendId { get; }

        /// <inheritdoc/>
        public BackendConfig Config { get; }

        /// <inheritdoc/>
        public void Start(AsyncSemaphore semaphore)
        {
            // Start background work, wake up for every interval(set in backend config).
            this.backgroundPollingLoopTask = TaskScheduler.Current.Run(() => this.ProbeEndpointsAsync(semaphore, this.cts.Token));
        }

        /// <inheritdoc/>
        public async Task StopAsync()
        {
            // Generate Cancellation token.
            this.cts.Cancel();

            // Wait until this prober is done, gracefully shut down the probing process.
            await this.backgroundPollingLoopTask;

            // Release the cancellation source
            this.cts.Dispose();
            this.logger.LogInformation($"The backend prober for '{this.BackendId}' has stopped.");
        }

        /// <summary>
        /// Async methods that conduct http request to query the health state from the health controller of every endpoints.
        /// </summary>
        private async Task ProbeEndpointsAsync(AsyncSemaphore semaphore, CancellationToken cancellationToken)
        {
            // Always continue probing until receives cancellation token.
            try
            {
                while (true)
                {
                    List<Task> probeTasks = new List<Task>();
                    cancellationToken.ThrowIfCancellationRequested();

                    // try catch to prevent while loop crashed by any nonfatal exception
                    try
                    {
                        // Submit probe requests.
                        foreach (var endpoint in this.endpointManager.GetItems())
                        {
                            // Start a single probing attempt.
                            probeTasks.Add(this.ProbeEndpointAsync(endpoint, semaphore, cancellationToken));
                        }

                        await Task.WhenAll(probeTasks);
                    }
                    catch (OperationCanceledException) when (this.cts.IsCancellationRequested)
                    {
                        // If the cancel requested by our StopAsync method. It is a expected graceful shut down.
                        throw;
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        // Swallow the nonfatal exception, we don not want the health check to break.
                        this.logger.LogError(ex, $"Prober for '{this.BackendId}' encounters unexpected exception.");
                    }

                    this.logger.LogInformation($"The backend prober for '{this.BackendId}' has checked all endpoints with time interval {this.healthCheckInterval.TotalSeconds} second.");

                    // Wait for next probe cycle.
                    await this.timer.Delay(this.healthCheckInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (this.cts.IsCancellationRequested)
            {
                // If the cancel requested by our StopAsync method. It is a expected graceful shut down.
                this.logger.LogInformation($"Prober for backend '{this.BackendId}' has gracefully shutdown.");
            }
            catch (Exception ex)
            {
                // Swallow the exception, we want the health check continuously running like the heartbeat.
                this.logger.LogError(ex, $"Prober for '{this.BackendId}' encounters unexpected exception.");
            }
        }

        /// <summary>
        /// A probe attempt to one endpoint.
        /// </summary>
        private async Task ProbeEndpointAsync(EndpointInfo endpoint, AsyncSemaphore semaphore, CancellationToken cancellationToken)
        {
            // Conduct a dither for every endpoint probe to optimize concurrency.
            var randomDither = this.randomFactory.CreateRandomInstance();
            await this.timer.Delay(TimeSpan.FromMilliseconds(randomDither.Next(ditheringIntervalInMilliseconds)), cancellationToken);

            HealthProbeOutcome outcome = HealthProbeOutcome.Unknown;
            string logDetail = null;

            // Enforce max concurrency.
            await semaphore.WaitAsync();
            this.logger.LogInformation($"The backend prober for backend: '{this.BackendId}', endpoint: `{endpoint.EndpointId}` has started.");
            try
            {
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token))
                {
                    // Set up timeout and start probing.
                    timeoutCts.CancelAfter(httpTimeoutInterval, this.timer);
                    var response = await this.operationLogger.ExecuteAsync(
                            "IslandGateway.Core.Service.HealthProbe",
                            () => this.backendProbeHttpClient.GetAsync(new Uri(new Uri(endpoint.Config.Value.Address, UriKind.Absolute), this.healthControllerUrl), timeoutCts.Token));

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
                if (this.cts.IsCancellationRequested)
                {
                    outcome = HealthProbeOutcome.Canceled;
                    logDetail = "Operation deliberately canceled";
                    throw;
                }
                else
                {
                    outcome = HealthProbeOutcome.Timeout;
                    logDetail = $"Health probe timed out after {this.Config.HealthCheckOptions.Interval.TotalSeconds} second";
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Prober for '{endpoint.EndpointId}' encounters unexpected exception.", ex);
            }
            finally
            {
                if (outcome != HealthProbeOutcome.Canceled)
                {
                    // Update the health state base on the response.
                    var healthState = outcome == HealthProbeOutcome.Success ? EndpointHealth.Healthy : EndpointHealth.Unhealthy;
                    endpoint.DynamicState.Value = new EndpointDynamicState(healthState);
                    this.logger.LogInformation($"Health probe result for endpoint '{endpoint.EndpointId}': {outcome}. Details: {logDetail}");
                }

                // The probe operation is done, release the semaphore to allow other probes to proceed.
                semaphore.Release();
            }
        }
    }
}
