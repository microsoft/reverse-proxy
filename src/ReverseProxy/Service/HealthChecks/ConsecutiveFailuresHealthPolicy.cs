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
            var threshold = GetFailureThreshold(cluster);
            var changed = false;
            for (var i = 0; i < probingResults.Count; i++)
            {
                var destination = probingResults[i].Destination;

                var count = _failureCounters.GetOrCreateValue(destination);
                var newHealth = EvaluateHealthState(threshold, probingResults[i].Response, count);

                var state = destination.DynamicState;
                if (newHealth != state.Health.Active)
                {
                    // TODO: Should this use the same pattern as cluster state updates? This has consistency issues.
                    // E.g. track active and passive separately and create a new dynamic state as a composite as needed.
                    destination.DynamicState = new DestinationDynamicState(state.Health.ChangeActive(newHealth));
                    changed = true;
                    if (newHealth == DestinationHealth.Unhealthy)
                    {
                        Log.ActiveDestinationHealthStateIsSetToUnhealthy(_logger, destination.DestinationId, cluster.ClusterId);
                    }
                    else
                    {
                        Log.ActiveDestinationHealthStateIsSet(_logger, destination.DestinationId, cluster.ClusterId, newHealth);
                    }
                }
            }

            if (changed)
            {
                cluster.UpdateDynamicState();
            }
        }

        private double GetFailureThreshold(ClusterInfo cluster)
        {
            var thresholdEntry = _clusterThresholds.GetValue(cluster, c => new ParsedMetadataEntry<double>(TryParse, c, ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName));
            return thresholdEntry.GetParsedOrDefault(_options.DefaultThreshold);
        }

        private DestinationHealth EvaluateHealthState(double threshold, HttpResponseMessage response, AtomicCounter count)
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
