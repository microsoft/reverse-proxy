// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Routing;
using Yarp.ReverseProxy.SessionAffinity;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Management
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

            builder.Services.TryAddSingleton<IClusterDestinationsUpdater, ClusterDestinationsUpdater>();
            builder.Services.TryAddEnumerable(new[] {
                ServiceDescriptor.Singleton<IAvailableDestinationsPolicy, HealthyAndUnknownDestinationsPolicy>(),
                ServiceDescriptor.Singleton<IAvailableDestinationsPolicy, HealthyOrPanicDestinationsPolicy>()
            });
            return builder;
        }

        public static IReverseProxyBuilder AddConfigManager(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<ProxyConfigManager>();
            return builder;
        }

        public static IReverseProxyBuilder AddProxy(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IForwarderHttpClientFactory, ForwarderHttpClientFactory>();

            builder.Services.AddHttpForwarder();
            return builder;
        }

        public static IReverseProxyBuilder AddLoadBalancingPolicies(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IRandomFactory, RandomFactory>();

            builder.Services.TryAddEnumerable(new[] {
                ServiceDescriptor.Singleton<ILoadBalancingPolicy, FirstLoadBalancingPolicy>(),
                ServiceDescriptor.Singleton<ILoadBalancingPolicy, LeastRequestsLoadBalancingPolicy>(),
                ServiceDescriptor.Singleton<ILoadBalancingPolicy, RandomLoadBalancingPolicy>(),
                ServiceDescriptor.Singleton<ILoadBalancingPolicy, PowerOfTwoChoicesLoadBalancingPolicy>(),
                ServiceDescriptor.Singleton<ILoadBalancingPolicy, RoundRobinLoadBalancingPolicy>()
            });

            return builder;
        }

        public static IReverseProxyBuilder AddSessionAffinityProvider(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddEnumerable(new[] {
                ServiceDescriptor.Singleton<IAffinityFailurePolicy, RedistributeAffinityFailurePolicy>(),
                ServiceDescriptor.Singleton<IAffinityFailurePolicy, Return503ErrorAffinityFailurePolicy>()
            });
            builder.Services.TryAddEnumerable(new[] {
                ServiceDescriptor.Singleton<ISessionAffinityProvider, CookieSessionAffinityProvider>(),
                ServiceDescriptor.Singleton<ISessionAffinityProvider, CustomHeaderSessionAffinityProvider>()
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
