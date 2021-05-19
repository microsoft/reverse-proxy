// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal abstract class EventListenerService<TService, TTelemetryConsumer, TMetricsConsumer> : EventListener, IHostedService
    {
        protected abstract string EventSourceName { get; }

        protected readonly ILogger<TService> Logger;
        protected readonly TMetricsConsumer[]? MetricsConsumers;
        protected readonly TTelemetryConsumer[]? TelemetryConsumers;

        private EventSource? _eventSource;
        private readonly object _syncObject = new();
        private readonly bool _initialized;

        public EventListenerService(
            ILogger<TService> logger,
            IEnumerable<TTelemetryConsumer> telemetryConsumers,
            IEnumerable<TMetricsConsumer> metricsConsumers)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ = telemetryConsumers ?? throw new ArgumentNullException(nameof(telemetryConsumers));
            _ = metricsConsumers ?? throw new ArgumentNullException(nameof(metricsConsumers));

            TelemetryConsumers = telemetryConsumers.ToArray();
            MetricsConsumers = metricsConsumers.ToArray();

            if (TelemetryConsumers.Any(s => s is null) || metricsConsumers.Any(c => c is null))
            {
                throw new ArgumentException("A consumer may not be null",
                    TelemetryConsumers.Any(s => s is null) ? nameof(telemetryConsumers) : nameof(metricsConsumers));
            }

            if (TelemetryConsumers.Length == 0)
            {
                TelemetryConsumers = null;
            }

            if (MetricsConsumers.Length == 0)
            {
                MetricsConsumers = null;
            }

            lock (_syncObject)
            {
                if (_eventSource is EventSource eventSource)
                {
                    EnableEventSource(eventSource);
                }

                _initialized = true;
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == EventSourceName)
            {
                lock (_syncObject)
                {
                    _eventSource = eventSource;

                    if (_initialized)
                    {
                        // Ctor already finished - enable the EventSource here
                        EnableEventSource(eventSource);
                    }
                }
            }
        }

        private void EnableEventSource(EventSource eventSource)
        {
            var enableEvents = TelemetryConsumers is not null;
            var enableMetrics = MetricsConsumers is not null;

            if (!enableEvents && !enableMetrics)
            {
                return;
            }

            var eventLevel = enableEvents ? EventLevel.Verbose : EventLevel.Critical;
            var arguments = enableMetrics ? new Dictionary<string, string?> { { "EventCounterIntervalSec", MetricsOptions.Interval.TotalSeconds.ToString() } } : null;

            EnableEvents(eventSource, eventLevel, EventKeywords.None, arguments);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
