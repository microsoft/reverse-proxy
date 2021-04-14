// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal abstract class EventListenerService<TService, TTelemetryConsumer, TMetricsConsumer> : EventListener, IHostedService
    {
        protected abstract string EventSourceName { get; }

        protected readonly ILogger<TService> Logger;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly IHttpContextAccessor HttpContextAccessor;

        private EventSource _eventSource;
        private readonly ManualResetEventSlim _initializedMre = new();
        private readonly int _ctorThreadId = Environment.CurrentManagedThreadId;
        private readonly bool _enableEvents;
        private readonly bool _enableMetrics;

        public EventListenerService(ILogger<TService> logger, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor, ServiceCollectionInternal services)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            HttpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _ = services ?? throw new ArgumentNullException(nameof(services));

            _enableEvents = services.Services.Any(s => s.ServiceType == typeof(TTelemetryConsumer));
            _enableMetrics = services.Services.Any(s => s.ServiceType == typeof(TMetricsConsumer));

            lock (this)
            {
                if (_eventSource is not null)
                {
                    EnableEventSource();
                }

                // A different thread could be waiting for the ctor to finish, signal it that we're done
                _initializedMre.Set();
                _initializedMre = null;
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == EventSourceName)
            {
                lock (this)
                {
                    _eventSource = eventSource;

                    if (_initializedMre is null)
                    {
                        // Ctor already finished - enable the EventSource here
                        EnableEventSource();
                    }
                }

                // Ensure that the constructor finishes before exiting this method (so that the first events aren't dropped)
                // It's possible that we are executing as a part of the base ctor - check the Thread ID to avoid a deadlock
                if (Environment.CurrentManagedThreadId != _ctorThreadId)
                {
                    var mre = _initializedMre;
                    if (mre is not null)
                    {
                        // This wait will generally be very short (waiting for the ctor on a different thread to finish)
                        // If the ctor throws, the MRE will never be released - add a short timeout
                        mre.Wait(TimeSpan.FromSeconds(15));
                        mre.Dispose();
                    }
                }
            }
        }

        private void EnableEventSource()
        {
            if (!_enableEvents && !_enableMetrics)
            {
                return;
            }

            var eventLevel = _enableEvents ? EventLevel.LogAlways : EventLevel.Critical;
            var arguments = _enableMetrics ? new Dictionary<string, string> { { "EventCounterIntervalSec", MetricsOptions.Interval.TotalSeconds.ToString() } } : null;

            EnableEvents(_eventSource, eventLevel, EventKeywords.None, arguments);
            _eventSource = null;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
