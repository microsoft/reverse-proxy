// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ConsecutiveFailuresHealthPolicy : ActiveHealthCheckPolicyBase, IDestinationChangeListener
    {
        private readonly ConsecutiveFailuresHealthPolicyOptions _options;
        private readonly ConcurrentDictionary<DestinationInfo, FailureCounter> _failures = new ConcurrentDictionary<DestinationInfo, FailureCounter>();

        public ConsecutiveFailuresHealthPolicy(IOptions<ConsecutiveFailuresHealthPolicyOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public override string Name => HealthCheckConstants.ActivePolicy.ConsecutiveFailures;

        public void OnDestinationAdded(DestinationInfo destination)
        {
        }

        public void OnDestinationChanged(DestinationInfo destination)
        {
        }

        public void OnDestinationRemoved(DestinationInfo destination)
        {
            _failures.TryRemove(destination, out _);
        }

        protected override DestinationHealth EvaluateFailedProbe(ClusterConfig cluster, DestinationInfo destination, HttpResponseMessage response, Exception exception)
        {
            var count = _failures.GetOrAdd(destination, d => new FailureCounter());
            count.Increment();
            return count.IsHealthy(cluster, _options.DefaultThreshold) ? DestinationHealth.Healthy : DestinationHealth.Unhealthy;
        }

        protected override DestinationHealth EvaluateSuccessfulProbe(ClusterConfig cluster, DestinationInfo destination, HttpResponseMessage response)
        {
            _failures.TryRemove(destination, out _);
            return DestinationHealth.Healthy;
        }

        private class FailureCounter
        {
            private readonly ParsedMetadataEntry<double> _threshold = new ParsedMetadataEntry<double>(TryParse);
            private int _count;

            public void Increment()
            {
                Interlocked.Increment(ref _count);
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
