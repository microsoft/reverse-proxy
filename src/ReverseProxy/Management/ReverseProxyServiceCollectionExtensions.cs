// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Configuration.ConfigProvider;
using Yarp.ReverseProxy.Management;
using Yarp.ReverseProxy.Proxy;
using Yarp.ReverseProxy.Routing;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions for <see cref="IServiceCollection"/>
    /// used to register the ReverseProxy's components.
    /// </summary>
    public static class ReverseProxyServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the <see cref="IHttpProxy"/> service for direct proxying scenarios.
        /// </summary>
        public static IServiceCollection AddHttpProxy(this IServiceCollection services)
        {
            services.TryAddSingleton<IClock, Clock>();
            services.TryAddSingleton<IHttpProxy, HttpProxy>();
            services.TryAddSingleton<ITransformBuilder, TransformBuilder>();
            return services;
        }

        /// <summary>
        /// Adds ReverseProxy's services to Dependency Injection.
        /// </summary>
        public static IReverseProxyBuilder AddReverseProxy(this IServiceCollection services)
        {
            var builder = new ReverseProxyBuilder(services);
            builder
                .AddConfigBuilder()
                .AddRuntimeStateManagers()
                .AddConfigManager()
                .AddSessionAffinityProvider()
                .AddActiveHealthChecks()
                .AddPassiveHealthCheck()
                .AddLoadBalancingPolicies()
                .AddProxy();

            services.TryAddSingleton<ProxyEndpointFactory>();

            services.AddDataProtection();
            services.AddAuthorization();
            services.AddCors();
            services.AddRouting();

            return builder;
        }

        /// <summary>
        /// Loads routes and endpoints from config.
        /// </summary>
        public static IReverseProxyBuilder LoadFromConfig(this IReverseProxyBuilder builder, IConfiguration config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            builder.Services.AddSingleton<IProxyConfigProvider>(sp =>
            {
                // This is required because we're capturing the configuration via a closure
                return new ConfigurationConfigProvider(sp.GetRequiredService<ILogger<ConfigurationConfigProvider>>(), config);
            });

            return builder;
        }

        /// <summary>
        /// Registers a singleton IProxyConfigFilter service. Multiple filters are allowed and they will be run in registration order.
        /// </summary>
        /// <typeparam name="TService">A class that implements IProxyConfigFilter.</typeparam>
        public static IReverseProxyBuilder AddConfigFilter<TService>(this IReverseProxyBuilder builder) where TService : class, IProxyConfigFilter
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProxyConfigFilter, TService>());
            return builder;
        }

        /// <summary>
        /// Provides a callback that will be run for each route to conditionally add transforms.
        /// <see cref="AddTransforms(IReverseProxyBuilder, Action{TransformBuilderContext})"/> can be called multiple times to
        /// provide multiple callbacks.
        /// </summary>
        public static IReverseProxyBuilder AddTransforms(this IReverseProxyBuilder builder, Action<TransformBuilderContext> action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            builder.Services.AddSingleton<ITransformProvider>(new ActionTransformProvider(action));
            return builder;
        }

        /// <summary>
        /// Provides a <see cref="ITransformProvider"/> implementation that will be run for each route to conditionally add transforms.
        /// <see cref="AddTransforms{T}(IReverseProxyBuilder)"/> can be called multiple times to provide multiple distinct types.
        /// </summary>
        public static IReverseProxyBuilder AddTransforms<T>(this IReverseProxyBuilder builder) where T : class, ITransformProvider
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ITransformProvider, T>());
            return builder;
        }

        /// <summary>
        /// Adds a <see cref="ITransformFactory"/> implementation that will be used to read route transform config and generate
        /// the associated transform actions. <see cref="AddTransformFactory{T}(IReverseProxyBuilder)"/> can be called multiple
        /// times to provide multiple distinct types.
        /// </summary>
        public static IReverseProxyBuilder AddTransformFactory<T>(this IReverseProxyBuilder builder) where T : class, ITransformFactory
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ITransformFactory, T>());
            return builder;
        }

        /// <summary>
        /// Provides a callback to customize <see cref="SocketsHttpHandler"/> settings used for proxying requests.
        /// This will be called each time a cluster is added or changed. Cluster settings are applied to the handler before
        /// the callback. Custom data can be provided in the cluster metadata.
        /// </summary>
        public static IReverseProxyBuilder ConfigureHttpClient(this IReverseProxyBuilder builder, Action<ProxyHttpClientContext, SocketsHttpHandler> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.Services.AddSingleton<IProxyHttpClientFactory>(services =>
            {
                var logger = services.GetRequiredService<ILogger<ProxyHttpClientFactory>>();
                return new CallbackProxyHttpClientFactory(logger, configure);
            });
            return builder;
        }
    }
}
