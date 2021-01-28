// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Configuration.DependencyInjection;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

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
            builder.Services.AddSingleton<ICertificateConfigLoader, CertificateConfigLoader>();
            builder.Services.AddSingleton<IProxyConfigProvider>(sp =>
            {
                // This is required because we're capturing the configuration via a closure
                return new ConfigurationConfigProvider(sp.GetService<ILogger<ConfigurationConfigProvider>>(), config, sp.GetService<ICertificateConfigLoader>());
            });

            return builder;
        }

        /// <summary>
        /// Registers a singleton IProxyConfigFilter service. Multiple filters are allowed and they will be run in registration order.
        /// </summary>
        /// <typeparam name="TService">A class that implements IProxyConfigFilter.</typeparam>
        public static IReverseProxyBuilder AddProxyConfigFilter<TService>(this IReverseProxyBuilder builder) where TService : class, IProxyConfigFilter
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.AddSingleton<IProxyConfigFilter, TService>();
            return builder;
        }

        /// <summary>
        /// Provides a callback that will be run for each route to conditionally add transforms.
        /// <see cref="AddTransforms(IReverseProxyBuilder, Action{TransformBuilderContext})"/> can be called multiple times to
        /// provide multiple callbacks.
        /// </summary>
        public static IReverseProxyBuilder AddTransforms(this IReverseProxyBuilder builder, Action<TransformBuilderContext> action)
        {
            builder.Services.AddSingleton<ITransformFilter>(new CustomTransformFilter(action));
            return builder;
        }

        /// <summary>
        /// Provides a <see cref="ITransformFilter"/> implementation that will be run for each route to conditionally add transforms.
        /// <see cref="AddTransforms{T}(IReverseProxyBuilder)"/> can be called multiple times to provide multiple distinct types.
        /// </summary>
        public static IReverseProxyBuilder AddTransforms<T>(this IReverseProxyBuilder builder) where T : ITransformFilter
        {
            builder.Services.TryAddEnumerable(new[] {
                new ServiceDescriptor(typeof(ITransformFilter), typeof(T), ServiceLifetime.Singleton),
            });
            return builder;
        }

        /// <summary>
        /// Adds a <see cref="ITransformFactory"/> implementation that will be used to read route transform config and generate
        /// the associated transform actions. <see cref="AddTransformFactory{T}(IReverseProxyBuilder)"/> can be called multiple
        /// times to provide multiple distinct types.
        /// </summary>
        public static IReverseProxyBuilder AddTransformFactory<T>(this IReverseProxyBuilder builder) where T : ITransformFactory
        {
            builder.Services.TryAddEnumerable(new[] {
                new ServiceDescriptor(typeof(ITransformFactory), typeof(T), ServiceLifetime.Singleton),
            });
            return builder;
        }
    }
}
