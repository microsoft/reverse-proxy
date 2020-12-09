// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.ServiceFabric
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
        public static IReverseProxyBuilder LoadFromServiceFabric(this IReverseProxyBuilder builder)
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

            return builder;
        }
    }
}
