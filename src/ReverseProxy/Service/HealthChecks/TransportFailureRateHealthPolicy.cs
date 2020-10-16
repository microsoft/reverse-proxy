// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Calculates a proxied request failure rate for each destination and marks it as unhealthy if the specified limit is exceeded.
    /// </summary>
    /// <remarks>
    /// Rate is calculated as a percentage of failured requests to the total number of request proxied to a destination in the given period of time. Failed and total counters are tracked
    /// in a sliding time window which means that only the recent readings fitting in the window are taken into account. The window is implemented as a linked-list of timestamped records
    /// where each record contains the difference from the previous one in the number of failed and total requests. Additionally, there are 2 destination-wide counters storing aggregated values
    /// to enable a fast calculation of the current failure rate. When a new proxied request is reported, its status firstly affects those 2 aggregated counters and then also gets put
    /// in the record history. Once some record moves out of the detection time window, the failed and total counter deltas stored on it get subtracted from the respective aggregated counters.
    /// </remarks>
    internal class TransportFailureRateHealthPolicy : IPassiveHealthCheckPolicy
    {
        private static readonly TimeSpan _defaultReactivationPeriod = TimeSpan.FromSeconds(60);
        private readonly IReactivationScheduler _reactivationScheduler;
        private readonly TransportFailureRateHealthPolicyOptions _policyOptions;
        private readonly IUptimeClock _clock;
        private readonly string _propertyKey = nameof(TransportFailureRateHealthPolicy);

        public string Name => HealthCheckConstants.PassivePolicy.TransportFailureRate;

        public TransportFailureRateHealthPolicy(IOptions<TransportFailureRateHealthPolicyOptions> policyOptions, IUptimeClock clock, IReactivationScheduler reactivationScheduler)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _policyOptions = policyOptions?.Value ?? throw new ArgumentNullException(nameof(policyOptions));
            _reactivationScheduler = reactivationScheduler ?? throw new ArgumentNullException(nameof(reactivationScheduler));
        }

        public void RequestProxied(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error)
        {
            var newHealth = EvaluateProxiedRequest(cluster, destination, error != null);
            UpdateDestinationHealth(cluster, destination, newHealth);
        }

        private DestinationHealth EvaluateProxiedRequest(ClusterConfig cluster, DestinationInfo destination, bool failed)
        {
            var failureHistory = destination.GetOrAddProperty(_propertyKey, k => new ProxiedRequestHistory());
            lock (failureHistory)
            {
                failureHistory.AddNew(_clock.TickCount, (long)_policyOptions.DetectionWindowSize.TotalMilliseconds, failed);
                return failureHistory.IsHealthy(cluster, _policyOptions.DefaultFailureRateLimit) ? DestinationHealth.Healthy : DestinationHealth.Unhealthy;
            }
        }

        private void UpdateDestinationHealth(ClusterConfig cluster, DestinationInfo destination, DestinationHealth newPassiveHealth)
        {
            var state = destination.DynamicState;
            if (newPassiveHealth != state.Health.Passive)
            {
                destination.DynamicState = new DestinationDynamicState(state.Health.ChangePassive(newPassiveHealth));

                if (newPassiveHealth == DestinationHealth.Unhealthy)
                {
                    _reactivationScheduler.Schedule(destination, cluster.HealthCheckOptions.Passive.ReactivationPeriod ?? _defaultReactivationPeriod);
                }
            }
        }

        private class ProxiedRequestHistory
        {
            private const long RecordWindowSize = 1000;
            private long _nextRecordCreatedAt;
            private long _nextRecordTotalCount;
            private long _nextRecordFailedCount;
            private long _failedCount;
            private long _totalCount;
            private readonly ParsedMetadataEntry<double> _clusterRateLimit = new ParsedMetadataEntry<double>(TryParse);
            private readonly Queue<HistoryRecord> _records = new Queue<HistoryRecord>();

            public double Rate { get; private set; }

            public void AddNew(long eventTime, long detectionWindowSize, bool failed)
            {
                if (_nextRecordCreatedAt == 0)
                {
                    // Initialization.
                    _nextRecordCreatedAt = eventTime + RecordWindowSize;
                }

                // Don't create a new record on each event because it can negatively affect performance.
                // Instead, accumulate failed and total request counts reported during some period
                // and then add only one record storing them.
                if (eventTime >= _nextRecordCreatedAt)
                {
                    _records.Enqueue(new HistoryRecord(eventTime, _nextRecordTotalCount, _nextRecordFailedCount));
                    _nextRecordCreatedAt = eventTime + RecordWindowSize;
                    _nextRecordTotalCount = 0;
                    _nextRecordFailedCount = 0;
                }

                _nextRecordTotalCount++;
                _totalCount++;
                if (failed)
                {
                    _failedCount++;
                    _nextRecordFailedCount++;
                }

                while (_records.Count > 0 && (eventTime - _records.Peek().RecordedAt > detectionWindowSize))
                {
                    var removed = _records.Dequeue();
                    _failedCount -= removed.FailedCount;
                    _totalCount -= removed.TotalCount;
                }

                Rate = _totalCount == 0 ? 0.0 : _failedCount / _totalCount;
            }

            public bool IsHealthy(ClusterConfig cluster, double defaultFailureRateLimit)
            {
                return Rate < _clusterRateLimit.GetParsedOrDefault(cluster, TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, defaultFailureRateLimit);
            }

            private static bool TryParse(string stringValue, out double parsedValue)
            {
                return double.TryParse(stringValue, out parsedValue);
            }

            private readonly struct HistoryRecord
            {
                public HistoryRecord(long recordedAt, long totalCount, long failedCount)
                {
                    RecordedAt = recordedAt;
                    TotalCount = totalCount;
                    FailedCount = failedCount;
                }

                public long RecordedAt { get; }

                public long TotalCount { get; }

                public long FailedCount { get; }
            }
        }
    }
}
