// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Configuration.Contract;
using Microsoft.ReverseProxy.Configuration.DependencyInjection;
using Microsoft.ReverseProxy.Service;
using Microsoft.ReverseProxy.Service.Proxy;

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
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddHttpProxy(this IServiceCollection services)
        {
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
                .AddTelemetryShims()
                .AddConfigBuilder()
                .AddRuntimeStateManagers()
                .AddConfigManager()
                .AddSessionAffinityProvider()
                .AddProxy()
                .AddBackgroundWorkers();

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
            builder.Services.Configure<ConfigurationData>(config);
            builder.Services.AddSingleton<ICertificateConfigLoader, CertificateConfigLoader>();
            builder.Services.AddSingleton<IProxyConfigProvider, ConfigurationConfigProvider>();

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
    }
}
