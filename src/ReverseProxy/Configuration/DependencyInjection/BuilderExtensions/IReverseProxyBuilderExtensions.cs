// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.HealthChecks;
using Microsoft.ReverseProxy.Service.LoadBalancing;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Service.Routing;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Utilities;
using System.Linq;

namespace Microsoft.ReverseProxy.Configuration.DependencyInjection
{
    internal static class IReverseProxyBuilderExtensions
    {
        public static IReverseProxyBuilder AddConfigBuilder(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IConfigValidator, ConfigValidator>();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, HeaderMatcherPolicy>());
            return builder;
        }

        public static IReverseProxyBuilder AddRuntimeStateManagers(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IDestinationManagerFactory, DestinationManagerFactory>();
            builder.Services.TryAddSingleton<IClusterManager, ClusterManager>();
            builder.Services.TryAddSingleton<IRouteManager, RouteManager>();
            builder.Services.TryAddSingleton<ITimerFactory, TimerFactory>();
            builder.Services.TryAddSingleton<IDestinationHealthUpdater, DestinationHealthUpdater>();
            return builder;
        }

        public static IReverseProxyBuilder AddConfigManager(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<ProxyConfigManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddProxy(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<ITransformBuilder, TransformBuilder>();
            builder.Services.TryAddSingleton<IProxyHttpClientFactory, ProxyHttpClientFactory>();
            builder.Services.TryAddSingleton<IRandomFactory, RandomFactory>();

            builder.Services.AddHttpProxy();
            return builder;
        }

        public static IReverseProxyBuilder AddLoadBalancingPolicies(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IRandomFactory, RandomFactory>();

            builder.Services.TryAddEnumerable(new[] {
                new ServiceDescriptor(typeof(ILoadBalancingPolicy), typeof(FirstLoadBalancingPolicy), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(ILoadBalancingPolicy), typeof(LeastRequestsLoadBalancingPolicy), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(ILoadBalancingPolicy), typeof(RandomLoadBalancingPolicy), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(ILoadBalancingPolicy), typeof(PowerOfTwoChoicesLoadBalancingPolicy), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(ILoadBalancingPolicy), typeof(RoundRobinLoadBalancingPolicy), ServiceLifetime.Singleton)
            });

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

        public static IReverseProxyBuilder AddActiveHealthChecks(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IProbingRequestFactory, DefaultProbingRequestFactory>();

            // Avoid registering several IActiveHealthCheckMonitor implementations.
            if (!builder.Services.Any(d => d.ServiceType == typeof(IActiveHealthCheckMonitor)))
            {
                builder.Services.AddSingleton<ActiveHealthCheckMonitor>();
                builder.Services.AddSingleton<IActiveHealthCheckMonitor>(p => p.GetRequiredService<ActiveHealthCheckMonitor>());
                builder.Services.AddSingleton<IClusterChangeListener>(p => p.GetRequiredService<ActiveHealthCheckMonitor>());
            }

            builder.Services.AddSingleton<IActiveHealthCheckPolicy, ConsecutiveFailuresHealthPolicy>();
            return builder;
        }

        public static IReverseProxyBuilder AddPassiveHealthCheck(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IPassiveHealthCheckPolicy, TransportFailureRateHealthPolicy>();
            return builder;
        }
    }
}
