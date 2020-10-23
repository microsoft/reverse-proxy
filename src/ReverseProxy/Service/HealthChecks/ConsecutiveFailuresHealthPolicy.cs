// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ConsecutiveFailuresHealthPolicy : IActiveHealthCheckPolicy
    {
        private readonly ConsecutiveFailuresHealthPolicyOptions _options;
        private readonly ConditionalWeakTable<ClusterInfo, ParsedMetadataEntry<double>> _clusterThresholds = new ConditionalWeakTable<ClusterInfo, ParsedMetadataEntry<double>>();
        private readonly ConditionalWeakTable<DestinationInfo, AtomicCounter> _failureCounters = new ConditionalWeakTable<DestinationInfo, AtomicCounter>();
        private readonly ILogger<ConsecutiveFailuresHealthPolicy> _logger;

        public string Name => HealthCheckConstants.ActivePolicy.ConsecutiveFailures;

        public ConsecutiveFailuresHealthPolicy(IOptions<ConsecutiveFailuresHealthPolicyOptions> options, ILogger<ConsecutiveFailuresHealthPolicy> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ProbingCompleted(ClusterInfo cluster, IReadOnlyList<DestinationProbingResult> probingResults)
        {
            cluster.PauseHealthyDestinationUpdates();

            try
            {
                var clusterConfig = cluster.Config;
                for (var i = 0; i < probingResults.Count; i++)
                {
                    var destination = probingResults[i].Destination;

                    var count = _failureCounters.GetOrCreateValue(destination);
                    var newHealth = EvaluateHealthState(cluster, clusterConfig, probingResults[i].Response, count);

                    var state = destination.DynamicState;
                    if (newHealth != state.Health.Active)
                    {
                        destination.DynamicState = new DestinationDynamicState(state.Health.ChangeActive(newHealth));

                        if(newHealth == DestinationHealth.Unhealthy)
                        {
                            Log.ActiveDestinationHealthStateIsSetToUnhealthy(_logger, destination.DestinationId, cluster.ClusterId);
                        }
                        else
                        {
                            Log.ActiveDestinationHealthStateIsSet(_logger, destination.DestinationId, cluster.ClusterId, newHealth);
                        }
                    }
                }
            }
            finally
            {
                cluster.ResumeHealthyDestinationUpdates();
            }
        }

        private DestinationHealth EvaluateHealthState(ClusterInfo cluster, ClusterConfig clusterConfig, HttpResponseMessage response, AtomicCounter count)
        {
            DestinationHealth newHealth;
            if (response != null && response.IsSuccessStatusCode)
            {
                // Success
                count.Reset();
                newHealth = DestinationHealth.Healthy;
            }
            else
            {
                // Failure
                var currentFailureCount = count.Increment();
                var metadataName = ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName;
                if (!_clusterThresholds.TryGetValue(cluster, out var thresholdEntry))
                {
                    thresholdEntry = new ParsedMetadataEntry<double>(TryParse);
                    _clusterThresholds.AddOrUpdate(cluster, thresholdEntry);
                }
                var threshold = thresholdEntry.GetParsedOrDefault(clusterConfig, metadataName, _options.DefaultThreshold);
                newHealth = currentFailureCount < threshold ? DestinationHealth.Healthy : DestinationHealth.Unhealthy;
            }

            return newHealth;
        }

        private static bool TryParse(string stringValue, out double parsedValue)
        {
            return double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, string, Exception> _activeDestinationHealthStateIsSetToUnhealthy = LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                EventIds.ActiveDestinationHealthStateIsSetToUnhealthy,
                "Active health state of destination `{destinationId}` on cluster `{clusterId}` is set to 'unhealthy'.");

            private static readonly Action<ILogger, string, string, DestinationHealth, Exception> _activeDestinationHealthStateIsSet = LoggerMessage.Define<string, string, DestinationHealth>(
                LogLevel.Information,
                EventIds.ActiveDestinationHealthStateIsSet,
                "Active health state of destination `{destinationId}` on cluster `{clusterId}` is set to '{newHealthState}'.");

            public static void ActiveDestinationHealthStateIsSetToUnhealthy(ILogger logger, string destinationId, string clusterId)
            {
                _activeDestinationHealthStateIsSetToUnhealthy(logger, destinationId, clusterId, null);
            }

            public static void ActiveDestinationHealthStateIsSet(ILogger logger, string destinationId, string clusterId, DestinationHealth newHealthState)
            {
                _activeDestinationHealthStateIsSet(logger, destinationId, clusterId, newHealthState, null);
            }
        }
    }
}
