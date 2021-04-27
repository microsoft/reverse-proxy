// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service;
using Yarp.ReverseProxy.Service.Config;
using Yarp.ReverseProxy.Service.HealthChecks;
using Yarp.ReverseProxy.Service.LoadBalancing;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Service.Proxy.Infrastructure;
using Yarp.ReverseProxy.Service.Routing;
using Yarp.ReverseProxy.Service.SessionAffinity;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration.DependencyInjection
{
    internal static class IReverseProxyBuilderExtensions
    {
        public static IReverseProxyBuilder AddConfigBuilder(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IConfigValidator, ConfigValidator>();
            builder.Services.TryAddSingleton<IRandomFactory, RandomFactory>();
            builder.AddTransformFactory<ForwardedTransformFactory>();
            builder.AddTransformFactory<HttpMethodTransformFactory>();
            builder.AddTransformFactory<PathTransformFactory>();
            builder.AddTransformFactory<QueryTransformFactory>();
            builder.AddTransformFactory<RequestHeadersTransformFactory>();
            builder.AddTransformFactory<ResponseTransformFactory>();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, HeaderMatcherPolicy>());
            return builder;
        }

        public static IReverseProxyBuilder AddRuntimeStateManagers(this IReverseProxyBuilder builder)
        {
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
            builder.Services.TryAddSingleton<IProxyHttpClientFactory, ProxyHttpClientFactory>();

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
            builder.AddTransforms<AffinitizeTransformProvider>();

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
