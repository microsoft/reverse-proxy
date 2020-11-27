// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Hosting;
using Microsoft.ReverseProxy.Telemetry.Consumption;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IReverseProxyBuilderTelemetryExtensions
    {
        public static IReverseProxyBuilder AddTelemetryListeners(this IReverseProxyBuilder builder)
        {
            builder.AddProxyTelemetryListener();
            builder.AddKestrelTelemetryListener();
            builder.AddHttpTelemetryListener();
            builder.AddNameResolutionTelemetryListener();
            builder.AddNetSecurityTelemetryListener();
            builder.AddSocketsTelemetryListener();
            return builder;
        }

        public static IReverseProxyBuilder AddProxyTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.TryAddHostedService<ProxyEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddKestrelTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.TryAddHostedService<KestrelEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddHttpTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.TryAddHostedService<HttpEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddNameResolutionTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.TryAddHostedService<NameResolutionEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddNetSecurityTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.TryAddHostedService<NetSecurityEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddSocketsTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.TryAddHostedService<SocketsEventListenerService>();
            return builder;
        }

        private static void TryAddHostedService<T>(this IServiceCollection services)
            where T : class, IHostedService
        {
            foreach (var descriptor in services)
            {
                if (descriptor.ImplementationType == typeof(T))
                {
                    return;
                }
            }

            services.AddHostedService<T>();
        }
    }
}
