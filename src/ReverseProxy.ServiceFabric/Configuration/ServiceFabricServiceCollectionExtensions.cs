// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Discovery;

namespace Yarp.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// Extensions for <see cref="IServiceCollection"/>
    /// used to register Service Fabric integration components.
    /// </summary>
    public static class ServiceFabricServiceCollectionExtensions
    {
        /// <summary>
        /// Uses Service Fabric dynamic service discovery as the configuration source for the Proxy
        /// via a specific implementation of <see cref="IProxyConfigProvider" />.
        /// </summary>
        public static IReverseProxyBuilder LoadFromServiceFabric(this IReverseProxyBuilder builder, IConfiguration configuration)
        {
            _ = configuration ?? throw new ArgumentNullException(nameof(configuration));

            AddServices(builder);

            builder.Services.Configure<ServiceFabricDiscoveryOptions>(configuration);

            return builder;
        }

        /// <summary>
        /// Uses Service Fabric dynamic service discovery as the configuration source for the Proxy
        /// via a specific implementation of <see cref="IProxyConfigProvider" />.
        /// </summary>
        public static IReverseProxyBuilder LoadFromServiceFabric(this IReverseProxyBuilder builder, Action<ServiceFabricDiscoveryOptions> configureOptions)
        {
            _ = configureOptions ?? throw new ArgumentNullException(nameof(configureOptions));

            AddServices(builder);

            builder.Services.Configure(configureOptions);

            return builder;
        }

        private static void AddServices(IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IFabricClientWrapper, FabricClientWrapper>();
            builder.Services.AddSingleton<IQueryClientWrapper, QueryClientWrapper>();
            builder.Services.AddSingleton<IPropertyManagementClientWrapper, PropertyManagementClientWrapper>();
            builder.Services.AddSingleton<IServiceManagementClientWrapper, ServiceManagementClientWrapper>();
            builder.Services.AddSingleton<IHealthClientWrapper, HealthClientWrapper>();
            builder.Services.AddSingleton<ICachedServiceFabricCaller, CachedServiceFabricCaller>();
            builder.Services.AddSingleton<IServiceExtensionLabelsProvider, ServiceExtensionLabelsProvider>();
            builder.Services.AddSingleton<IDiscoverer, Discoverer>();
            builder.Services.AddSingleton<IProxyConfigProvider, ServiceFabricConfigProvider>();
        }
    }
}
