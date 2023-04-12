// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;
using System.Diagnostics;

namespace Yarp.ReverseProxy.Health;

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
        if (config is not null && config.Enabled.GetValueOrDefault())
        {
            Scheduler.ScheduleEntity(cluster, config.Interval ?? _monitorOptions.DefaultInterval);
        }
    }

    public void OnClusterChanged(ClusterState cluster)
    {
        var config = cluster.Model.Config.HealthCheck?.Active;
        if (config is not null && config.Enabled.GetValueOrDefault())
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
        var config = cluster.Model.Config.HealthCheck?.Active;
        if (config is null || !config.Enabled.GetValueOrDefault())
        {
            return;
        }

        var activity = Observability.YarpActivitySource.StartActivity("Proxy active destination health check", ActivityKind.Internal);

        Log.StartingActiveHealthProbingOnCluster(_logger, cluster.ClusterId);

        var allDestinations = cluster.DestinationsState.AllDestinations;
        var probeTasks = new Task<DestinationProbingResult>[allDestinations.Count];
        var probeResults = new DestinationProbingResult[probeTasks.Length];

        var timeout = config.Timeout ?? _monitorOptions.DefaultTimeout;

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
            var policy = _policies.GetRequiredServiceById(config.Policy, HealthCheckConstants.ActivePolicy.ConsecutiveFailures);
            policy.ProbingCompleted(cluster, probeResults);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            Log.ActiveHealthProbingFailedOnCluster(_logger, cluster.ClusterId, ex);
            activity?.SetStatus(ActivityStatusCode.Error);
        }
        finally
        {
            try
            {
                foreach (var probeResult in probeResults)
                {
                    probeResult.Response?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOccuredDuringActiveHealthProbingShutdownOnCluster(_logger, cluster.ClusterId, ex);
            }

            Log.StoppedActiveHealthProbingOnCluster(_logger, cluster.ClusterId);
            activity?.Stop();
        }
    }

    private async Task<DestinationProbingResult> ProbeDestinationAsync(ClusterState cluster, DestinationState destination, TimeSpan timeout)
    {
        HttpRequestMessage request;
        try
        {
            request = _probingRequestFactory.CreateRequest(cluster.Model, destination.Model);
        }
        catch (Exception ex)
        {
            Log.ActiveHealthProbeConstructionFailedOnCluster(_logger, destination.DestinationId, cluster.ClusterId, ex);

            return new DestinationProbingResult(destination, null, ex);
        }

        var probeActivity = Observability.YarpActivitySource.StartActivity("Proxy destination health check", ActivityKind.Client);
        probeActivity?.AddTag("Cluster ID", cluster.ClusterId);
        probeActivity?.AddTag("Destination ID", destination.DestinationId);
        var cts = new CancellationTokenSource(timeout);
        try
        {
            Log.SendingHealthProbeToEndpointOfDestination(_logger, request.RequestUri, destination.DestinationId, cluster.ClusterId);
            var response = await cluster.Model.HttpClient.SendAsync(request, cts.Token);
            Log.DestinationProbingCompleted(_logger, destination.DestinationId, cluster.ClusterId, (int)response.StatusCode);

            probeActivity?.SetStatus(ActivityStatusCode.Ok);
            probeActivity?.Stop();

            return new DestinationProbingResult(destination, response, null);
        }
        catch (Exception ex)
        {
            Log.DestinationProbingFailed(_logger, destination.DestinationId, cluster.ClusterId, ex);

            probeActivity?.SetStatus(ActivityStatusCode.Error);
            probeActivity?.Stop();

            return new DestinationProbingResult(destination, null, ex);
        }
        finally
        {
            cts.Dispose();
        }
    }
}
