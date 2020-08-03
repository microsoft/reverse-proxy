// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Metrics;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Telemetry;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Configuration.DependencyInjection
{
    internal static class IReverseProxyBuilderExtensions
    {
        public static IReverseProxyBuilder AddTelemetryShims(this IReverseProxyBuilder builder)
        {
            // NOTE: Consumers of ReverseProxy are expected to replace these with their own classes
            builder.Services.TryAddSingleton<IMetricCreator, NullMetricCreator>();
            builder.Services.TryAddSingleton(typeof(IOperationLogger<>), typeof(TextOperationLogger<>));
            return builder;
        }

        public static IReverseProxyBuilder AddMetrics(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<ProxyMetrics>();
            return builder;
        }

        public static IReverseProxyBuilder AddConfigBuilder(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IDynamicConfigBuilder, DynamicConfigBuilder>();
            builder.Services.TryAddSingleton<IRouteValidator, RouteValidator>();
            builder.Services.TryAddSingleton<IRuntimeRouteBuilder, RuntimeRouteBuilder>();
            return builder;
        }

        public static IReverseProxyBuilder AddRuntimeStateManagers(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IDestinationManagerFactory, DestinationManagerFactory>();
            builder.Services.TryAddSingleton<IClusterManager, ClusterManager>();
            builder.Services.TryAddSingleton<IRouteManager, RouteManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddConfigManager(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IProxyConfigManager, ReverseProxyConfigManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddProxy(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<ITransformBuilder, TransformBuilder>();
            builder.Services.TryAddSingleton<IProxyHttpClientFactoryFactory, ProxyHttpClientFactoryFactory>();
            builder.Services.TryAddSingleton<ILoadBalancer, LoadBalancer>();
            builder.Services.TryAddSingleton<IRandomFactory, RandomFactory>();
            builder.Services.TryAddSingleton<IHttpProxy, HttpProxy>();
            return builder;
        }

        public static IReverseProxyBuilder AddBackgroundWorkers(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IMonotonicTimer, MonotonicTimer>();

            return builder;
        }

        public static IReverseProxyBuilder AddSessionAffinityProvider(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddEnumerable(new[] {
                new ServiceDescriptor(typeof(IAffinityFailurePolicy), typeof(RedistributeAffinityFailurePolicy), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(IAffinityFailurePolicy), typeof(Return503ErrorAffinityFailurePolicy), ServiceLifetime.Singleton)
            });
            builder.Services.TryAddEnumerable(new[] {
                new ServiceDescriptor(typeof(ISessionAffinityProvider), typeof(CookieSessionAffinityProvider), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(ISessionAffinityProvider), typeof(CustomHeaderSessionAffinityProvider), ServiceLifetime.Singleton)
            });

            return builder;
        }
    }
}
