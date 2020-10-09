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
using Microsoft.ReverseProxy.Service.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class TransportFailureRateHealthPolicy : PassiveHealthCheckPolicyBase, IDestinationChangeListener
    {
        private readonly TransportFailureRateHealthPolicyOptions _policyOptions;
        private readonly ConcurrentDictionary<DestinationInfo, FailureHistory> _failures = new ConcurrentDictionary<DestinationInfo, FailureHistory>();
        private readonly IUptimeClock _clock;

        public override string Name => HealthCheckConstants.PassivePolicy.TransportFailureRate;

        public TransportFailureRateHealthPolicy(IOptions<TransportFailureRateHealthPolicyOptions> policyOptions, IUptimeClock clock, IReactivationScheduler reactivationScheduler)
            : base(reactivationScheduler)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _policyOptions = policyOptions?.Value ?? throw new ArgumentNullException(nameof(policyOptions));
        }

        protected override DestinationHealth EvaluateFailedRequest(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error)
        {
            var failureHistory = _failures.GetOrAdd(destination, d => new FailureHistory());
            lock (failureHistory)
            {
                failureHistory.AddNewFailure(_clock.TickCount, _policyOptions.DetectionWindowSize);
                return failureHistory.IsHealthy(cluster, _policyOptions.DefaultFailureRateLimit) ? DestinationHealth.Healthy : DestinationHealth.Unhealthy;
            }
        }

        protected override DestinationHealth EvaluateSuccessfulRequest(ClusterConfig cluster, DestinationInfo destination, HttpContext context)
        {
            // Simply return the current health state because a successful request cannot affect it.
            return destination.DynamicState.Health.Passive;
        }

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

        private class FailureHistory
        {
            private const long RecordWindowSize = 1000;
            private long _nextRecordUpdatedAt;
            private long _nextRecordCount;
            private long _totalCount;
            private string _clusterRateLimitString;
            private double _clusterRateLimit;
            private readonly Queue<FailureRecord> _records = new Queue<FailureRecord>();

            public double Rate { get; private set; }

            public void AddNewFailure(long eventTime, long detectionWindowSize)
            {
                if (eventTime - _nextRecordUpdatedAt > RecordWindowSize)
                {
                    _records.Enqueue(new FailureRecord(_nextRecordUpdatedAt, _nextRecordCount));
                    _nextRecordCount = 0;
                }

                _nextRecordUpdatedAt = eventTime;
                _nextRecordCount++;
                _totalCount++;

                while (_records.Count > 0 && (eventTime - _records.Peek().FailedAt > detectionWindowSize))
                {
                    var removed = _records.Dequeue();
                    _totalCount -= removed.Count;
                }

                Rate = _totalCount == 0 ? 0.0 : _totalCount / detectionWindowSize;
            }

            public bool IsHealthy(ClusterConfig cluster, double defaultFailureRateLimit)
            {
                if (cluster.Metadata != null && cluster.Metadata.TryGetValue(TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, out var metadataRateLimitString))
                {
                    if (_clusterRateLimitString != metadataRateLimitString)
                    {
                        _clusterRateLimit = double.TryParse(metadataRateLimitString, out var metadataRateLimit) ? metadataRateLimit : defaultFailureRateLimit;
                        _clusterRateLimitString = metadataRateLimitString;
                    }
                }
                else
                {
                    _clusterRateLimit = defaultFailureRateLimit;
                }

                return Rate < _clusterRateLimit;
            }

            private readonly struct FailureRecord
            {
                public FailureRecord(long failedAt, long count)
                {
                    FailedAt = failedAt;
                    Count = count;
                }

                public long FailedAt { get; }

                public long Count { get; }
            }
        }
    }
}
