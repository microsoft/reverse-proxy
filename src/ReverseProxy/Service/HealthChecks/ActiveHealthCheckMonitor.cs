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

        public ActiveHealthCheckMonitor(IOptions<ActiveHealthCheckMonitorOptions> monitorOptions, IEnumerable<IActiveHealthCheckPolicy> policies, IUptimeClock clock)
        {
            _monitorOptions = monitorOptions?.Value ?? throw new ArgumentNullException(nameof(monitorOptions));
            _policies = policies.ToDictionaryByUniqueId(p => p.Name);
            _scheduler = new EntityActionScheduler<ClusterInfo>(async cluster => await ProbeCluster(cluster), false, clock);
        }

        public async Task ForceCheckAll(IEnumerable<ClusterInfo> allClusters)
        {
            var probeClusterTasks = new List<Task>();
            foreach (var cluster in allClusters)
            {
                if (cluster.Config.Value.HealthCheckOptions.Active.Enabled)
                {
                    probeClusterTasks.Add(ProbeCluster(cluster));
                }
            }

            await Task.WhenAll(probeClusterTasks);
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
            var probeTasks = new List<Task<HttpResponseMessage>>();
            foreach (var destination in allDestinations)
            {
                var probeAddress = new Uri(!string.IsNullOrEmpty(destination.Config.Health) ? destination.Config.Health : destination.Config.Address, UriKind.Absolute);
                var probePath = clusterConfig.HealthCheckOptions.Active.Path;
                var probeUri = new Uri(probeAddress, probePath);
                var request = new HttpRequestMessage(HttpMethod.Get, probeUri);
                var timeout = clusterConfig.HealthCheckOptions.Active.Timeout ?? _monitorOptions.DefaultTimeout;
                probeTasks.Add(clusterConfig.HttpClient.SendAsync(request, new CancellationTokenSource(timeout).Token));
            }

            for (var i = 0; i < allDestinations.Count; i++)
            {
                try
                {
                    var response = await probeTasks[i].ConfigureAwait(false);
                    policy.ProbingCompleted(clusterConfig, allDestinations[i], response, null);
                }
                catch (Exception e)
                {
                    policy.ProbingCompleted(clusterConfig, allDestinations[i], null, e);
                }
            }
        }
    }
}
