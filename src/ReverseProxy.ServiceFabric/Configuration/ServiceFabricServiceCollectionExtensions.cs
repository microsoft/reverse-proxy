// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        /// Adds the services needed to integrate Service Fabric with the Island Gateway to Dependency Injection.
        /// </summary>
        public static IReverseProxyBuilder AddServiceFabricDiscovery(this IReverseProxyBuilder builder)
        {
            builder.Services.AddSingleton<IQueryClientWrapper, QueryClientWrapper>();
            builder.Services.AddSingleton<IPropertyManagementClientWrapper, PropertyManagementClientWrapper>();
            builder.Services.AddSingleton<IServiceManagementClientWrapper, ServiceManagementClientWrapper>();
            builder.Services.AddSingleton<IHealthClientWrapper, HealthClientWrapper>();
            builder.Services.AddSingleton<IServiceFabricCaller, CachedServiceFabricCaller>();
            builder.Services.AddSingleton<IServiceExtensionLabelsProvider, ServiceExtensionLabelsProvider>();
            builder.Services.AddSingleton<IDiscoverer, Discoverer>();
            builder.Services.AddSingleton<IProxyConfigProvider, ServiceFabricConfigProvider>();

            return builder;
        }
    }
}
