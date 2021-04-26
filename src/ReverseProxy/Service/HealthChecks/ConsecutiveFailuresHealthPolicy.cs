// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    internal sealed class ConsecutiveFailuresHealthPolicy : IActiveHealthCheckPolicy
    {
        private readonly ConsecutiveFailuresHealthPolicyOptions _options;
        private readonly ConditionalWeakTable<ClusterInfo, ParsedMetadataEntry<double>> _clusterThresholds = new ConditionalWeakTable<ClusterInfo, ParsedMetadataEntry<double>>();
        private readonly ConditionalWeakTable<DestinationInfo, AtomicCounter> _failureCounters = new ConditionalWeakTable<DestinationInfo, AtomicCounter>();
        private readonly IDestinationHealthUpdater _healthUpdater;

        public string Name => HealthCheckConstants.ActivePolicy.ConsecutiveFailures;

        public ConsecutiveFailuresHealthPolicy(IOptions<ConsecutiveFailuresHealthPolicyOptions> options, IDestinationHealthUpdater healthUpdater)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _healthUpdater = healthUpdater ?? throw new ArgumentNullException(nameof(healthUpdater));
        }

        public void ProbingCompleted(ClusterInfo cluster, IReadOnlyList<DestinationProbingResult> probingResults)
        {
            if (probingResults.Count == 0)
            {
                return;
            }

            var threshold = GetFailureThreshold(cluster);

            var newHealthStates = new NewActiveDestinationHealth[probingResults.Count];
            for (var i = 0; i < probingResults.Count; i++)
            {
                var destination = probingResults[i].Destination;

                var count = _failureCounters.GetOrCreateValue(destination);
                var newHealth = EvaluateHealthState(threshold, probingResults[i].Response, count);
                newHealthStates[i] = new NewActiveDestinationHealth(destination, newHealth);
            }

            _healthUpdater.SetActive(cluster, newHealthStates);
        }

        private double GetFailureThreshold(ClusterInfo cluster)
        {
            var thresholdEntry = _clusterThresholds.GetValue(cluster, c => new ParsedMetadataEntry<double>(TryParse, c, ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName));
            return thresholdEntry.GetParsedOrDefault(_options.DefaultThreshold);
        }

        private static DestinationHealth EvaluateHealthState(double threshold, HttpResponseMessage response, AtomicCounter count)
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
                var currentFailureCount = count.IncrementAndGetValue();
                newHealth = currentFailureCount < threshold ? DestinationHealth.Healthy : DestinationHealth.Unhealthy;
            }

            return newHealth;
        }

        private static bool TryParse(string stringValue, out double parsedValue)
        {
            return double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);
        }
    }
}
