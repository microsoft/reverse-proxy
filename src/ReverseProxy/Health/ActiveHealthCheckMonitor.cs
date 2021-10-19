// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health
{
    internal partial class ActiveHealthCheckMonitor : IActiveHealthCheckMonitor, IClusterChangeListener, IDisposable
    {
        private readonly ActiveHealthCheckMonitorOptions _monitorOptions;
        private readonly IDictionary<string, IActiveHealthCheckPolicy> _policies;
        private readonly IProbingRequestFactory _probingRequestFactory;
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
            Scheduler = new EntityActionScheduler<ClusterState>(cluster => ProbeCluster(cluster), autoStart: false, runOnce: false, timerFactory);
        }

        public bool InitialProbeCompleted { get; private set; }
        
        internal EntityActionScheduler<ClusterState> Scheduler { get; }

        public Task CheckHealthAsync(IEnumerable<ClusterState> clusters)
        {
            return Task.Run(async () =>
            {
                try
                {
                    var probeClusterTasks = new List<Task>();
                    foreach (var cluster in clusters)
                    {
                        if ((cluster.Model.Config.HealthCheck?.Active?.Enabled).GetValueOrDefault())
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
                finally
                {
                    InitialProbeCompleted = true;
                }

                Scheduler.Start();
            });
        }

        public void OnClusterAdded(ClusterState cluster)
        {
            var config = cluster.Model.Config.HealthCheck?.Active;
            if (config != null && config.Enabled.GetValueOrDefault())
            {
                Scheduler.ScheduleEntity(cluster, config.Interval ?? _monitorOptions.DefaultInterval);
            }
        }

        public void OnClusterChanged(ClusterState cluster)
        {
            var config = cluster.Model.Config.HealthCheck?.Active;
            if (config != null && config.Enabled.GetValueOrDefault())
            {
                Scheduler.ChangePeriod(cluster, config.Interval ?? _monitorOptions.DefaultInterval);
            }
            else
            {
                Scheduler.UnscheduleEntity(cluster);
            }
        }

        public void OnClusterRemoved(ClusterState cluster)
        {
            Scheduler.UnscheduleEntity(cluster);
        }

        public void Dispose()
        {
            Scheduler.Dispose();
        }

        private async Task ProbeCluster(ClusterState cluster)
        {
            var clusterModel = cluster.Model;
            var config = clusterModel.Config.HealthCheck?.Active;
            if (config == null || !config.Enabled.GetValueOrDefault())
            {
                return;
            }

            Log.StartingActiveHealthProbingOnCluster(_logger, cluster.ClusterId);

            var policy = _policies.GetRequiredServiceById(config.Policy, HealthCheckConstants.ActivePolicy.ConsecutiveFailures);
            var allDestinations = cluster.DestinationsState.AllDestinations;
            var timeout = config.Timeout ?? _monitorOptions.DefaultTimeout;
            var probeTasks = new Task<DestinationProbingResult>[allDestinations.Count];
            var probeResults = new DestinationProbingResult[probeTasks.Length];

            for (var i = 0; i < probeTasks.Length; i++)
            {
                probeTasks[i] = ProbeDestinationAsync(cluster, allDestinations[i], timeout);
            }

            for (var i = 0; i < probeResults.Length; i++)
            {
                probeResults[i] = await probeTasks[i];
            }

            try
            {
                policy.ProbingCompleted(cluster, probeResults);
            }
            catch (Exception ex)
            {
                Log.ActiveHealthProbingFailedOnCluster(_logger, cluster.ClusterId, ex);
            }
            finally
            {
                Log.StoppedActiveHealthProbingOnCluster(_logger, cluster.ClusterId);
            }
        }

        private async Task<DestinationProbingResult> ProbeDestinationAsync(ClusterState cluster, DestinationState destination, TimeSpan timeout)
        {
            var cts = new CancellationTokenSource(timeout);
            var waitingForSendAsync = false;
            try
            {
                var request = _probingRequestFactory.CreateRequest(cluster.Model, destination.Model);
                Log.SendingHealthProbeToEndpointOfDestination(_logger, request.RequestUri, destination.DestinationId, cluster.ClusterId);

                var responseTask = cluster.Model.HttpClient.SendAsync(request, cts.Token);
                waitingForSendAsync = true;

                var response = await responseTask;
                Log.DestinationProbingCompleted(_logger, destination.DestinationId, cluster.ClusterId, (int)response.StatusCode);

                return new DestinationProbingResult(destination, response, null);
            }
            catch (Exception ex)
            {
                if (waitingForSendAsync)
                {
                    Log.DestinationProbingFailed(_logger, destination.DestinationId, cluster.ClusterId, ex);
                }
                else
                {
                    Log.ActiveHealthProbeConstructionFailedOnCluster(_logger, destination.DestinationId, cluster.ClusterId, ex);
                }

                return new DestinationProbingResult(destination, null, ex);
            }
            finally
            {
                cts.Dispose();
            }
        }
    }
}
