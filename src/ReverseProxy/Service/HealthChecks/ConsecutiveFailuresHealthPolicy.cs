// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ConsecutiveFailuresHealthPolicy : IActiveHealthCheckPolicy
    {
        private readonly ConsecutiveFailuresHealthPolicyOptions _options;
        private readonly string _propertyKey = nameof(ConsecutiveFailuresHealthPolicy);

        public string Name => HealthCheckConstants.ActivePolicy.ConsecutiveFailures;

        public ConsecutiveFailuresHealthPolicy(IOptions<ConsecutiveFailuresHealthPolicyOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public void ProbingCompleted(ClusterInfo cluster, IReadOnlyList<DestinationProbingResult> probingResults)
        {
            cluster.PauseHealthyDestinationUpdates();

            var clusterConfig = cluster.Config;
            for (var i = 0; i < probingResults.Count; i++)
            {
                var destination = probingResults[i].Destination;

                var count = destination.GetOrAddProperty(_propertyKey, k => new FailureCounter());
                var newHealth = EvaluateHealthState(clusterConfig, probingResults[i].Response, count);

                var state = destination.DynamicState;
                if (newHealth != state.Health.Active)
                {
                    destination.DynamicState = new DestinationDynamicState(state.Health.ChangeActive(newHealth));
                }
            }

            cluster.ResumeHealthyDestinationUpdates();
        }

        private DestinationHealth EvaluateHealthState(ClusterConfig clusterConfig, HttpResponseMessage response, FailureCounter count)
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
                count.Increment();
                newHealth = count.IsHealthy(clusterConfig, _options.DefaultThreshold) ? DestinationHealth.Healthy : DestinationHealth.Unhealthy;
            }

            return newHealth;
        }

        private class FailureCounter
        {
            private readonly ParsedMetadataEntry<double> _threshold = new ParsedMetadataEntry<double>(TryParse);
            private int _count;

            public void Increment()
            {
                Interlocked.Increment(ref _count);
            }

            public void Reset()
            {
                Interlocked.Exchange(ref _count, 0);
            }

            public bool IsHealthy(ClusterConfig cluster, double defaultThreshold)
            {
                return _count >= _threshold.GetParsedOrDefault(cluster, ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName, defaultThreshold);
            }

            private static bool TryParse(string stringValue, out double parsedValue)
            {
                return double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);
            }
        }
    }
}
