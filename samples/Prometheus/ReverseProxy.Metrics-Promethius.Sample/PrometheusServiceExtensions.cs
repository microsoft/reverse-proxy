// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.Sample
{
    public static class PrometheusServiceExtensions
    {
        public static IServiceCollection AddPrometheusProxyMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IForwarderMetricsConsumer, PrometheusForwarderMetrics>();
            return services;
        }

#if NET
        public static IServiceCollection AddPrometheusDnsMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<INameResolutionMetricsConsumer, PrometheusDnsMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusKestrelMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IKestrelMetricsConsumer, PrometheusKestrelMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusOutboundHttpMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<IHttpMetricsConsumer, PrometheusOutboundHttpMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusSocketsMetrics(this IServiceCollection services)
        {
            services.AddTelemetryListeners();
            services.AddSingleton<ISocketsMetricsConsumer, PrometheusSocketMetrics>();
            return services;
        }
#endif

        public static IServiceCollection AddAllPrometheusMetrics(this IServiceCollection services)
        {
            services.AddPrometheusProxyMetrics();
#if NET
            services.AddPrometheusDnsMetrics();
            services.AddPrometheusKestrelMetrics();
            services.AddPrometheusOutboundHttpMetrics();
            services.AddPrometheusSocketsMetrics();
#endif
            return services;
        }
    }
}
