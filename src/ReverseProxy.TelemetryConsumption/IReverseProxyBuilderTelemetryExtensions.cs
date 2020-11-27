// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            builder.Services.AddHostedService<ProxyEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddKestrelTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHostedService<KestrelEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddHttpTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHostedService<HttpEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddNameResolutionTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHostedService<NameResolutionEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddNetSecurityTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHostedService<NetSecurityEventListenerService>();
            return builder;
        }

        public static IReverseProxyBuilder AddSocketsTelemetryListener(this IReverseProxyBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHostedService<SocketsEventListenerService>();
            return builder;
        }
    }
}
