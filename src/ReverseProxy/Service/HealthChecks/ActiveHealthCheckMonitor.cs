// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal partial class ActiveHealthCheckMonitor : IActiveHealthCheckMonitor, IClusterChangeListener, IDisposable
    {
        private readonly ActiveHealthCheckMonitorOptions _monitorOptions;
        private readonly IDictionary<string, IActiveHealthCheckPolicy> _policies;
        private readonly IProbingRequestFactory _probingRequestFactory;
        private readonly EntityActionScheduler<ClusterInfo> _scheduler;
        private readonly ILogger<ActiveHealthCheckMonitor> _logger;

        public ActiveHealthCheckMonitor(
            IOptions<ActiveHealthCheckMonitorOptions> monitorOptions,
            IEnumerable<IActiveHealthCheckPolicy> policies,
            IProbingRequestFactory probingRequestFactory,
            ITimerFactory timerFactory,
            ILogger<ActiveHealthCheckMonitor> logger)
        {
            _monitorOptions = monitorOptions?.Value ?? throw new ArgumentNullException(nameof(monitorOptions));
            _policies = policies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(policies));
            _probingRequestFactory = probingRequestFactory ?? throw new ArgumentNullException(nameof(probingRequestFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scheduler = new EntityActionScheduler<ClusterInfo>(cluster => ProbeCluster(cluster), autoStart: false, runOnce: false, timerFactory);
        }

        public Task CheckHealthAsync(IEnumerable<ClusterInfo> clusters)
        {
            return Task.Run(async () =>
            {
                try
                {
                    var probeClusterTasks = new List<Task>();
                    foreach (var cluster in clusters)
                    {
                        if (cluster.Config.HealthCheckOptions.Active.Enabled)
                        {
                            probeClusterTasks.Add(ProbeCluster(cluster));
                        }
                    }

                    await Task.WhenAll(probeClusterTasks);
                }
                catch (Exception ex)
                {
                    Log.ExplicitActiveCheckOfAllClustersHealthFailed(_logger, ex);
                }

                _scheduler.Start();
            });
        }

        public void OnClusterAdded(ClusterInfo cluster)
        {
            var activeHealthCheckOptions = cluster.Config.HealthCheckOptions.Active;
            if (activeHealthCheckOptions.Enabled)
            {
                _scheduler.ScheduleEntity(cluster, activeHealthCheckOptions.Interval ?? _monitorOptions.DefaultInterval);
            }
        }

        public void OnClusterChanged(ClusterInfo cluster)
        {
            var activeHealthCheckOptions = cluster.Config.HealthCheckOptions.Active;
            if (activeHealthCheckOptions.Enabled)
            {
                _scheduler.ChangePeriod(cluster, activeHealthCheckOptions.Interval ?? _monitorOptions.DefaultInterval);
            }
            else
            {
                _scheduler.UnscheduleEntity(cluster);
            }
        }

        public void OnClusterRemoved(ClusterInfo cluster)
        {
            _scheduler.UnscheduleEntity(cluster);
        }

        public void Dispose()
        {
            _scheduler.Dispose();
        }

        private async Task ProbeCluster(ClusterInfo cluster)
        {
            var clusterConfig = cluster.Config;
            if (!clusterConfig.HealthCheckOptions.Active.Enabled)
            {
                return;
            }

            Log.StartingActiveHealthProbingOnCluster(_logger, cluster.ClusterId);

            // Policy must always be present if the active health check is enabled for a cluster.
            // It's validated and ensured by a configuration validator.
            var policy = _policies.GetRequiredServiceById(clusterConfig.HealthCheckOptions.Active.Policy);
            var allDestinations = cluster.DynamicState.AllDestinations;
            var probeTasks = new List<(Task<HttpResponseMessage> Task, CancellationTokenSource Cts)>(allDestinations.Count);
            try
            {
                foreach (var destination in allDestinations)
                {
                    var timeout = clusterConfig.HealthCheckOptions.Active.Timeout ?? _monitorOptions.DefaultTimeout;
                    var cts = new CancellationTokenSource(timeout);
                    try
                    {
                        var request = _probingRequestFactory.CreateRequest(clusterConfig, destination.Config);

                        Log.SendingHealthProbeToEndpointOfDestination(_logger, request.RequestUri, destination.DestinationId, cluster.ClusterId);

                        probeTasks.Add((clusterConfig.HttpClient.SendAsync(request, cts.Token), cts));
                    }
                    catch (Exception ex)
                    {
                        // Log and suppress an exception to give a chance for all destinations to be probed.
                        Log.ActiveHealthProbeConstructionFailedOnCluster(_logger, destination.DestinationId, cluster.ClusterId, ex);

                        cts.Dispose();
                    }
                }

                var probingResults = new DestinationProbingResult[probeTasks.Count];
                for (var i = 0; i < probeTasks.Count; i++)
                {
                    HttpResponseMessage response = null;
                    ExceptionDispatchInfo edi = null;
                    try
                    {
                        response = await probeTasks[i].Task;
                        Log.DestinationProbingCompleted(_logger, allDestinations[i].DestinationId, cluster.ClusterId, (int)response.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        edi = ExceptionDispatchInfo.Capture(ex);
                        Log.DestinationProbingFailed(_logger, allDestinations[i].DestinationId, cluster.ClusterId, ex);
                    }
                    probingResults[i] = new DestinationProbingResult(allDestinations[i], response, edi?.SourceException);
                }

                policy.ProbingCompleted(cluster, probingResults);
            }
            catch (Exception ex)
            {
                Log.ActiveHealthProbingFailedOnCluster(_logger, cluster.ClusterId, ex);
            }
            finally
            {
                foreach (var probeTask in probeTasks)
                {
                    try
                    {
                        try
                        {
                            probeTask.Cts.Cancel();
                        }
                        catch (Exception ex)
                        {
                            // Suppress exceptions to ensure the task will be awaited.
                            Log.ErrorOccuredDuringActiveHealthProbingShutdownOnCluster(_logger, cluster.ClusterId, ex);
                        }

                        var response = await probeTask.Task;
                        response.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Suppress exceptions to ensure all responses get a chance to be disposed.
                        Log.ErrorOccuredDuringActiveHealthProbingShutdownOnCluster(_logger, cluster.ClusterId, ex);
                    }
                    finally
                    {
                        // Dispose CancellationTokenSource even if the response task threw an exception.
                        // Dispose() is not expected to throw here.
                        probeTask.Cts.Dispose();
                    }
                }

                Log.StoppedActiveHealthProbingOnCluster(_logger, cluster.ClusterId);
            }
        }
    }
}
