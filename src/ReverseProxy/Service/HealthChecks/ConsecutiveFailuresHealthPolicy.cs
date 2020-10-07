// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ConsecutiveFailuresHealthPolicy : ActiveHealthCheckPolicyBase, IDestinationChangeListener
    {
        private readonly ConsecutiveFailuresHealthPolicyOptions _options;
        private readonly ConcurrentDictionary<DestinationInfo, long> _failures = new ConcurrentDictionary<DestinationInfo, long>();

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
            var count = _failures.AddOrUpdate(destination, 1, (d, v) => v + 1);
            var threshold = cluster.Metadata.TryGetValue(ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName, out var metadataThresholdString)
                    && double.TryParse(metadataThresholdString, out var metadataThreshold) ? metadataThreshold : _options.DefaultThreshold;
            return count >= threshold ? DestinationHealth.Unhealthy : DestinationHealth.Healthy;
        }

        protected override DestinationHealth EvaluateSuccessfulProbe(ClusterConfig cluster, DestinationInfo destination, HttpResponseMessage response)
        {
            _failures.TryRemove(destination, out _);
            return DestinationHealth.Healthy;
        }
    }
}
