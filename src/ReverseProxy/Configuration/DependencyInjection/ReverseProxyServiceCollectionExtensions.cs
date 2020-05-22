// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.ReverseProxy.Configuration;
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
                .AddInMemoryRepos()
                .AddConfigBuilder()
                .AddRuntimeStateManagers()
                .AddConfigManager()
                .AddDynamicEndpointDataSource()
                .AddDataProtection()
                .AddSessionAffinityProvider()
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
            builder.Services.AddOptions().PostConfigure<ProxyConfigOptions>(options =>
            {
                foreach (var (id, backend) in options.Backends)
                {
                    // The Object style config binding puts the id as the key in the dictionary, but later we want it on the
                    // backend object as well.
                    backend.Id = id;
                }
            });
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
