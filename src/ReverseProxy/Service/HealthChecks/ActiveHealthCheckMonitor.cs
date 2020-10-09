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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ActiveHealthCheckMonitor : IActiveHealthCheckMonitor, IClusterChangeListener
    {
        private readonly ActiveHealthCheckMonitorOptions _monitorOptions;
        private readonly IDictionary<string, IActiveHealthCheckPolicy> _policies;
        private readonly EntityActionScheduler<ClusterInfo> _scheduler;
        private readonly IProxyAppState _proxyAppState;

        public ActiveHealthCheckMonitor(
            IOptions<ActiveHealthCheckMonitorOptions> monitorOptions,
            IEnumerable<IActiveHealthCheckPolicy> policies,
            IUptimeClock clock,
            IProxyAppState proxyAppState)
        {
            _monitorOptions = monitorOptions?.Value ?? throw new ArgumentNullException(nameof(monitorOptions));
            _proxyAppState = proxyAppState ?? throw new ArgumentNullException(nameof(proxyAppState));
            _policies = policies.ToDictionaryByUniqueId(p => p.Name);
            _scheduler = new EntityActionScheduler<ClusterInfo>(async cluster => await ProbeCluster(cluster, false), false, clock);
        }

        public void ForceCheckAll(IEnumerable<ClusterInfo> allClusters, Action callback)
        {
            Task.Run(async () =>
            {
                try
                {
                    var probeClusterTasks = new List<Task>();
                    foreach (var cluster in allClusters)
                    {
                        if (cluster.Config.Value.HealthCheckOptions.Active.Enabled)
                        {
                            probeClusterTasks.Add(ProbeCluster(cluster, true));
                        }
                    }

                    await Task.WhenAll(probeClusterTasks);
                }
                catch (Exception)
                {
                    // TODO: Add logging
                }

                callback();
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

        private async Task ProbeCluster(ClusterInfo cluster, bool force)
        {
            if (!force && !_proxyAppState.IsFullyInitialized)
            {
                // Do nothing until the proxy is fully initialized.
                return;
            }

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
                    var probeAddress = new Uri(!string.IsNullOrEmpty(destination.Config.Health) ? destination.Config.Health : destination.Config.Address, UriKind.Absolute);
                    var probePath = clusterConfig.HealthCheckOptions.Active.Path;
                    var probeUri = new Uri(probeAddress, probePath);
                    var request = new HttpRequestMessage(HttpMethod.Get, probeUri);
                    var timeout = clusterConfig.HealthCheckOptions.Active.Timeout ?? _monitorOptions.DefaultTimeout;
                    var cts = new CancellationTokenSource(timeout);
                    probeTasks.Add((clusterConfig.HttpClient.SendAsync(request, cts.Token), cts));
                }

                for (var i = 0; i < allDestinations.Count; i++)
                {
                    try
                    {
                        var response = await probeTasks[i].Task.ConfigureAwait(false);
                        policy.ProbingCompleted(clusterConfig, allDestinations[i], response, null);
                    }
                    catch (Exception e)
                    {
                        policy.ProbingCompleted(clusterConfig, allDestinations[i], null, e);
                    }
                }
            }
            finally
            {
                foreach(var probeTask in probeTasks)
                {
                    try
                    {
                        if (probeTask.Task.Exception == null && probeTask.Task.IsCompleted)
                        {
                            probeTask.Task.Result.Dispose();
                        }

                        probeTask.Cts.Dispose();
                    }
                    catch
                    {
                        // Suppress exceptions to ensure all responses get a chance to be disposed.
                    }
                }
            }
        }
    }
}
