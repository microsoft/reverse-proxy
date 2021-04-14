// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.Sample
{
    public static class PrometheusServiceExtensions
    {
        public static IServiceCollection AddPrometheusProxyMetrics(this IServiceCollection services)
        {
            services.AddProxyTelemetryListener();
            services.AddSingleton<IProxyMetricsConsumer, PrometheusProxyMetrics>();
            return services;
        }

#if NET5_0_OR_GREATER
        public static IServiceCollection AddPrometheusDnsMetrics(this IServiceCollection services)
        {
            services.AddSocketsTelemetryListener();
            services.AddSingleton<INameResolutionMetricsConsumer, PrometheusDnsMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusKestrelMetrics(this IServiceCollection services)
        {
            services.AddKestrelTelemetryListener();
            services.AddSingleton<IKestrelMetricsConsumer, PrometheusKestrelMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusOutboundHttpMetrics(this IServiceCollection services)
        {
            services.AddHttpTelemetryListener();
            services.AddSingleton<IHttpMetricsConsumer, PrometheusOutboundHttpMetrics>();
            return services;
        }

        public static IServiceCollection AddPrometheusSocketsMetrics(this IServiceCollection services)
        {
            services.AddSocketsTelemetryListener();
            services.AddSingleton<ISocketsMetricsConsumer, PrometheusSocketMetrics>();
            return services;
        }
#endif

        public static IServiceCollection AddAllPrometheusMetrics(this IServiceCollection services)
        {
            services.AddPrometheusProxyMetrics();
#if NET5_0_OR_GREATER
            services.AddPrometheusDnsMetrics();
            services.AddPrometheusKestrelMetrics();
            services.AddPrometheusOutboundHttpMetrics();
            services.AddPrometheusSocketsMetrics();
#endif
            return services;
        }

        public static IApplicationBuilder UsePerRequestMetricCollection(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PerRequestYarpMetricCollectionMiddleware>();
        }
    }

}

