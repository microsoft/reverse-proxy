// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Abstractions.Time;
using Microsoft.ReverseProxy.Common.Telemetry;
using Microsoft.ReverseProxy.Common.Util;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.Service;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.ReverseProxy.Core.Service.Metrics;
using Microsoft.ReverseProxy.Core.Service.Proxy;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Configuration.DependencyInjection
{
    internal static class Core
    {
        public static IReverseProxyBuilder AddTelemetryShims(this IReverseProxyBuilder builder)
        {
            // NOTE: Consumers of ReverseProxy are expected to replace these with their own classes
            builder.Services.TryAddSingleton<IMetricCreator, NullMetricCreator>();
            builder.Services.TryAddSingleton<IOperationLogger, TextOperationLogger>();
            return builder;
        }

        public static IReverseProxyBuilder AddMetrics(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<ProxyMetrics>();
            return builder;
        }

        public static IReverseProxyBuilder AddInMemoryRepos(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IBackendsRepo, InMemoryBackendsRepo>();
            builder.Services.TryAddSingleton<IRoutesRepo, InMemoryRoutesRepo>();

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
            builder.Services.TryAddSingleton<IEndpointManagerFactory, EndpointManagerFactory>();
            builder.Services.TryAddSingleton<IBackendManager, BackendManager>();
            builder.Services.TryAddSingleton<IRouteManager, RouteManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddConfigManager(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IReverseProxyConfigManager, ReverseProxyConfigManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddDynamicEndpointDataSource(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IProxyDynamicEndpointDataSource, ProxyDynamicEndpointDataSource>();
            return builder;
        }

        public static IReverseProxyBuilder AddProxy(this IReverseProxyBuilder builder)
        {
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
    }
}
