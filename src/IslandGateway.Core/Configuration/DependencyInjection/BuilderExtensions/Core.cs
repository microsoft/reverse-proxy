// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Abstractions.Time;
using IslandGateway.Common.Telemetry;
using IslandGateway.Common.Util;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.Service;
using IslandGateway.Core.Service.Management;
using IslandGateway.Core.Service.Metrics;
using IslandGateway.Core.Service.Proxy;
using IslandGateway.Core.Service.Proxy.Infra;
using Microsoft.Extensions.DependencyInjection;

namespace IslandGateway.Core.Configuration.DependencyInjection
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
            builder.Services.AddSingleton<IRouteParser, RouteParser>();
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
