// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
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

namespace Microsoft.ReverseProxy.Core.Configuration.DependencyInjection
{
    internal static class Core
    {
        public static IReverseProxyBuilder AddTelemetryShims(this IReverseProxyBuilder builder)
        {
            // NOTE: Consumers of ReverseProxy are expected to replace these with their own classes
            builder.Services.AddSingleton<IMetricCreator, NullMetricCreator>();
            builder.Services.AddSingleton<IOperationLogger, TextOperationLogger>();
            return builder;
        }

        public static IReverseProxyBuilder AddMetrics(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<ProxyMetrics>();
            return builder;
        }

        public static IReverseProxyBuilder AddInMemoryRepos(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IBackendsRepo, InMemoryBackendsRepo>();
            builder.Services.AddSingleton<IRoutesRepo, InMemoryRoutesRepo>();

            return builder;
        }

        public static IReverseProxyBuilder AddConfigBuilder(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IDynamicConfigBuilder, DynamicConfigBuilder>();
            builder.Services.AddSingleton<IRouteValidator, RouteValidator>();
            builder.Services.AddSingleton<IRuntimeRouteBuilder, RuntimeRouteBuilder>();
            return builder;
        }

        public static IReverseProxyBuilder AddRuntimeStateManagers(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IEndpointManagerFactory, EndpointManagerFactory>();
            builder.Services.AddSingleton<IBackendManager, BackendManager>();
            builder.Services.AddSingleton<IRouteManager, RouteManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddConfigManager(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IReverseProxyConfigManager, ReverseProxyConfigManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddDynamicEndpointDataSource(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IProxyDynamicEndpointDataSource, ProxyDynamicEndpointDataSource>();
            return builder;
        }

        public static IReverseProxyBuilder AddProxy(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IProxyHttpClientFactoryFactory, ProxyHttpClientFactoryFactory>();
            builder.Services.AddSingleton<ILoadBalancer, LoadBalancer>();
            builder.Services.AddSingleton<IHttpProxy, HttpProxy>();
            return builder;
        }

        public static IReverseProxyBuilder AddBackgroundWorkers(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IMonotonicTimer, MonotonicTimer>();

            return builder;
        }
    }
}
