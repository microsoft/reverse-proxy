// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.ReverseProxy.Core.Configuration;
using Microsoft.ReverseProxy.Core.Configuration.DependencyInjection;
using Microsoft.ReverseProxy.Core.Service;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions for <see cref="IServiceCollection"/>
    /// used to register the ReverseProxy's components.
    /// </summary>
    public static class ReverseProxyServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ReverseProxy's services to Dependency Injection.
        /// </summary>
        public static IReverseProxyBuilder AddReverseProxy(this IServiceCollection services)
        {
            var builder = new ReverseProxyBuilder(services);
            builder
                .AddTelemetryShims()
                .AddMetrics()
                .AddInMemoryRepos()
                .AddConfigBuilder()
                .AddRuntimeStateManagers()
                .AddConfigManager()
                .AddDynamicEndpointDataSource()
                .AddProxy()
                .AddBackgroundWorkers();

            return builder;
        }

        /// <summary>
        /// Loads routes and endpoints from config.
        /// </summary>
        public static IReverseProxyBuilder LoadFromConfig(this IReverseProxyBuilder builder, IConfiguration config)
        {
            builder.Services.Configure<ProxyConfigOptions>(config);
            builder.Services.AddHostedService<ProxyConfigLoader>();

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
