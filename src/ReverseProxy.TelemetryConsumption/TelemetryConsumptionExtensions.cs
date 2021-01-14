// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Telemetry.Consumption;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TelemetryConsumptionExtensions
    {
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

        public static IServiceCollection AddProxyTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<ProxyEventListenerService>();
            return services;
        }

        public static IServiceCollection AddKestrelTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<KestrelEventListenerService>();
            return services;
        }

#if NET5_0
        public static IServiceCollection AddHttpTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<HttpEventListenerService>();
            return services;
        }

        public static IServiceCollection AddNameResolutionTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<NameResolutionEventListenerService>();
            return services;
        }

        public static IServiceCollection AddNetSecurityTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<NetSecurityEventListenerService>();
            return services;
        }

        public static IServiceCollection AddSocketsTelemetryListener(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddHostedService<SocketsEventListenerService>();
            return services;
        }
#endif
    }
}
