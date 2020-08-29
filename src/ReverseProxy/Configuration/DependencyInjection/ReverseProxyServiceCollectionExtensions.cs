// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Configuration.Contract;
using Microsoft.ReverseProxy.Configuration.DependencyInjection;
using Microsoft.ReverseProxy.Service;

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
                .AddConfigBuilder()
                .AddRuntimeStateManagers()
                .AddConfigManager()
                .AddSessionAffinityProvider()
                .AddProxy()
                .AddBackgroundWorkers();

            services.AddDataProtection();
            services.AddAuthorization();
            services.AddCors();

            return builder;
        }

        /// <summary>
        /// Loads routes and endpoints from config.
        /// </summary>
        public static IReverseProxyBuilder LoadFromConfig(this IReverseProxyBuilder builder, IConfiguration config)
        {
            builder.Services.Configure<ConfigurationOptions>(config);
            builder.Services.AddOptions().PostConfigure<ConfigurationOptions>(options =>
            {
                foreach (var (id, cluster) in options.Clusters)
                {
                    // The Object style config binding puts the id as the key in the dictionary, but later we want it on the
                    // cluster object as well.
                    cluster.Id = id;
                }
            });
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
