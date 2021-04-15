// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal abstract class EventListenerService<TService, TTelemetryConsumer, TMetricsConsumer> : EventListener, IHostedService
    {
        // We need a way to signal to OnEventSourceCreated that the EventListenerService constructor finished
        // OnEventSourceCreated may be called before we even reach the derived ctor (as it's exposed from the base ctor)
        // Because of that, we can't assign the MRE as part of the ctor, we have to do it as part of the _initializedMre field initializer
        // But since the ctor itself may throw here, we need a way to observe the same MRE instance from outside the ctor
        // We pull the MRE from a ThreadStatic that a ctor wrapper (Create) can observe
        [ThreadStatic]
        private static ManualResetEventSlim _threadStaticInitializedMre;

        public static TEventListener Create<TEventListener>(IServiceProvider serviceProvider)
            where TEventListener : EventListenerService<TService, TTelemetryConsumer, TMetricsConsumer>
        {
            _threadStaticInitializedMre = new();
            try
            {
                return ActivatorUtilities.CreateInstance<TEventListener>(serviceProvider);
            }
            finally
            {
                _threadStaticInitializedMre.Set();
                _threadStaticInitializedMre = null;
            }
        }

        protected abstract string EventSourceName { get; }

        protected readonly ILogger<TService> Logger;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly IHttpContextAccessor HttpContextAccessor;

        private EventSource _eventSource;
        private readonly object _syncObject = new();
        private readonly ManualResetEventSlim _initializedMre = _threadStaticInitializedMre;
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

            lock (_syncObject)
            {
                if (_eventSource is not null)
                {
                    EnableEventSource();
                }

                Debug.Assert(_initializedMre is not null);
                _initializedMre = null;
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == EventSourceName)
            {
                lock (_syncObject)
                {
                    _eventSource = eventSource;

                    if (_initializedMre is null)
                    {
                        // Ctor already finished - enable the EventSource here
                        EnableEventSource();
                    }
                }

                // Ensure that the constructor finishes before exiting this method (so that the first events aren't dropped)
                // It's possible that we are executing as a part of the base ctor - only block if we're running on a different thread
                var mre = _initializedMre;
                if (mre is not null && !ReferenceEquals(mre, _threadStaticInitializedMre))
                {
                    mre.Wait();
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
