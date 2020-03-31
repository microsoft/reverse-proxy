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
        public static IIslandGatewayBuilder AddTelemetryShims(this IIslandGatewayBuilder builder)
        {
            // NOTE: Consumers of IslandGateway are expected to replace these with their own classes
            builder.Services.AddSingleton<IMetricCreator, NullMetricCreator>();
            builder.Services.AddSingleton<IOperationLogger, TextOperationLogger>();
            return builder;
        }

        public static IIslandGatewayBuilder AddMetrics(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<GatewayMetrics>();
            return builder;
        }

        public static IIslandGatewayBuilder AddInMemoryRepos(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<IBackendsRepo, InMemoryBackendsRepo>();
            builder.Services.AddSingleton<IBackendEndpointsRepo, InMemoryEndpointsRepo>();
            builder.Services.AddSingleton<IRoutesRepo, InMemoryRoutesRepo>();

            return builder;
        }

        public static IIslandGatewayBuilder AddConfigBuilder(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<IDynamicConfigBuilder, DynamicConfigBuilder>();
            builder.Services.AddSingleton<IRouteValidator, RouteValidator>();
            builder.Services.AddSingleton<IRuntimeRouteBuilder, RuntimeRouteBuilder>();
            return builder;
        }

        public static IIslandGatewayBuilder AddRuntimeStateManagers(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<IEndpointManagerFactory, EndpointManagerFactory>();
            builder.Services.AddSingleton<IBackendManager, BackendManager>();
            builder.Services.AddSingleton<IRouteManager, RouteManager>();
            return builder;
        }

        public static IIslandGatewayBuilder AddConfigManager(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<IIslandGatewayConfigManager, IslandGatewayConfigManager>();
            return builder;
        }

        public static IIslandGatewayBuilder AddDynamicEndpointDataSource(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<IGatewayDynamicEndpointDataSource, GatewayDynamicEndpointDataSource>();
            return builder;
        }

        public static IIslandGatewayBuilder AddProxy(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<IProxyHttpClientFactoryFactory, ProxyHttpClientFactoryFactory>();
            builder.Services.AddSingleton<ILoadBalancer, LoadBalancer>();
            builder.Services.AddSingleton<IProxyInvoker, ProxyInvoker>();
            builder.Services.AddSingleton<IHttpProxy, HttpProxy>();
            return builder;
        }

        public static IIslandGatewayBuilder AddBackgroundWorkers(this IIslandGatewayBuilder builder)
        {
            builder.Services.AddSingleton<IMonotonicTimer, MonotonicTimer>();

            return builder;
        }
    }
}
