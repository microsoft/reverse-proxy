// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TelemetryConsumptionExtensions
    {
#if NET5_0
        /// <summary>
        /// Shortcut for registering all (Proxy, Kestrel, Http, NameResolution, NetSecurity and Sockets) telemetry listeners.
        /// </summary>
#else
        /// <summary>
        /// Shortcut for registering all (Proxy and Kestrel) telemetry listeners.
        /// </summary>
#endif
        public static IServiceCollection AddTelemetryListeners(this IServiceCollection services)
        {
            services.AddProxyTelemetryListener();
            services.AddKestrelTelemetryListener();
#if NET5_0
            services.AddHttpTelemetryListener();
            services.AddNameResolutionTelemetryListener();
            services.AddNetSecurityTelemetryListener();
            services.AddSocketsTelemetryListener();
#endif
            return services;
        }

        /// <summary>
        /// Registers a service responsible for calling <see cref="IProxyTelemetryConsumer"/>s and <see cref="IProxyMetricsConsumer"/>s.
        /// </summary>
        public static IServiceCollection AddProxyTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<ProxyEventListenerService>();
            return services;
        }

#if NET5_0
        /// <summary>
        /// Registers a service responsible for calling <see cref="IKestrelTelemetryConsumer"/>s and <see cref="IKestrelMetricsConsumer"/>s.
        /// </summary>
#else
        /// <summary>
        /// Registers a service responsible for calling <see cref="IKestrelTelemetryConsumer"/>s
        /// </summary>
#endif
        public static IServiceCollection AddKestrelTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<KestrelEventListenerService>();
            return services;
        }

#if NET5_0
        /// <summary>
        /// Registers a service responsible for calling <see cref="IHttpTelemetryConsumer"/>s and <see cref="IHttpMetricsConsumer"/>s.
        /// </summary>
        public static IServiceCollection AddHttpTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<HttpEventListenerService>();
            return services;
        }

        /// <summary>
        /// Registers a service responsible for calling <see cref="INameResolutionTelemetryConsumer"/>s and <see cref="INameResolutionMetricsConsumer"/>s.
        /// </summary>
        public static IServiceCollection AddNameResolutionTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<NameResolutionEventListenerService>();
            return services;
        }

        /// <summary>
        /// Registers a service responsible for calling <see cref="INetSecurityTelemetryConsumer"/>s and <see cref="INetSecurityMetricsConsumer"/>s.
        /// </summary>
        public static IServiceCollection AddNetSecurityTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<NetSecurityEventListenerService>();
            return services;
        }

        /// <summary>
        /// Registers a service responsible for calling <see cref="ISocketsTelemetryConsumer"/>s and <see cref="ISocketsMetricsConsumer"/>s.
        /// </summary>
        public static IServiceCollection AddSocketsTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<SocketsEventListenerService>();
            return services;
        }
#endif
    }
}
