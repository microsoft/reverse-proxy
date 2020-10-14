// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    internal class ActiveHealthCheckMonitor : IActiveHealthCheckMonitor, IClusterChangeListener
    {
        private readonly ActiveHealthCheckMonitorOptions _monitorOptions;
        private readonly IDictionary<string, IActiveHealthCheckPolicy> _policies;
        private readonly IProbingRequestFactory _probingRequestFactory;
        private readonly EntityActionScheduler<ClusterInfo> _scheduler;
        private readonly IProxyAppState _proxyAppState;

        public ActiveHealthCheckMonitor(
            IOptions<ActiveHealthCheckMonitorOptions> monitorOptions,
            IEnumerable<IActiveHealthCheckPolicy> policies,
            IProbingRequestFactory probingRequestFactory,
            IUptimeClock clock,
            IProxyAppState proxyAppState)
        {
            _monitorOptions = monitorOptions?.Value ?? throw new ArgumentNullException(nameof(monitorOptions));
            _proxyAppState = proxyAppState ?? throw new ArgumentNullException(nameof(proxyAppState));
            _policies = policies.ToDictionaryByUniqueId(p => p.Name);
            _probingRequestFactory = probingRequestFactory ?? throw new ArgumentNullException(nameof(probingRequestFactory));
            _scheduler = new EntityActionScheduler<ClusterInfo>(async cluster => await ProbeCluster(cluster), autoStart: false, runOnce: false, clock);
        }

        public void ForceCheckAll()
        {
            Task.Run(async () =>
            {
                try
                {
                    var probeClusterTasks = new List<Task>();
                    foreach (var cluster in _scheduler.GetScheduledEntities())
                    {
                        if (cluster.Config.Value.HealthCheckOptions.Active.Enabled)
                        {
                            probeClusterTasks.Add(ProbeCluster(cluster));
                        }
                    }

                    await Task.WhenAll(probeClusterTasks);
                    _proxyAppState.SetFullyInitialized();
                    _scheduler.Start();
                }
                catch (Exception)
                {
                    // TODO: Add logging
                }
            });
        }

        public void OnClusterAdded(ClusterInfo cluster)
        {
            var activeHealthCheckOptions = cluster.Config.Value.HealthCheckOptions.Active;
            if (activeHealthCheckOptions.Enabled)
            {
                _scheduler.ScheduleEntity(cluster, activeHealthCheckOptions.Interval ?? _monitorOptions.DefaultInterval);
            }
        }

        public void OnClusterChanged(ClusterInfo cluster)
        {
            var activeHealthCheckOptions = cluster.Config.Value.HealthCheckOptions.Active;
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
            var clusterConfig = cluster.Config.Value;
            if (!clusterConfig.HealthCheckOptions.Active.Enabled)
            {
                return;
            }

            // Policy must always be present if the active health check is enabled for a cluster.
            // It's validated and ensured by a configuration validator.
            var policy = _policies.GetRequiredServiceById(clusterConfig.HealthCheckOptions.Active.Policy, clusterConfig.HealthCheckOptions.Active.Policy);
            var allDestinations = cluster.DynamicState.AllDestinations;
            var probeTasks = new List<(Task<HttpResponseMessage> Task, CancellationTokenSource Cts)>();
            try
            {
                foreach (var destination in allDestinations)
                {
                    var request = _probingRequestFactory.GetRequest(clusterConfig, destination);
                    var timeout = clusterConfig.HealthCheckOptions.Active.Timeout ?? _monitorOptions.DefaultTimeout;
                    var cts = new CancellationTokenSource(timeout);
                    probeTasks.Add((clusterConfig.HttpClient.SendAsync(request, cts.Token), cts));
                }

                for (var i = 0; i < allDestinations.Count; i++)
                {
                    HttpResponseMessage response = null;
                    ExceptionDispatchInfo edi = null;
                    try
                    {
                        response = await probeTasks[i].Task.ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        edi = ExceptionDispatchInfo.Capture(e);
                    }
                    // TBD: Add bulk update here.
                    policy.ProbingCompleted(clusterConfig, allDestinations[i], response, edi?.SourceException);
                }
            }
            finally
            {
                foreach(var probeTask in probeTasks)
                {
                    try
                    {
                        var response = await probeTask.Task;
                        response.Dispose();
                    }
                    catch
                    {
                        // Suppress exceptions to ensure all responses get a chance to be disposed.
                    }
                    finally
                    {
                        probeTask.Cts.Dispose();
                    }
                }
            }
        }
    }
}
