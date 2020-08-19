// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            builder.Services.TryAddSingleton<IQueryClientWrapper, QueryClientWrapper>();
            builder.Services.TryAddSingleton<IPropertyManagementClientWrapper, PropertyManagementClientWrapper>();
            builder.Services.TryAddSingleton<IServiceManagementClientWrapper, ServiceManagementClientWrapper>();
            builder.Services.TryAddSingleton<IHealthClientWrapper, HealthClientWrapper>();
            builder.Services.TryAddSingleton<IServiceFabricCaller, CachedServiceFabricCaller>();
            builder.Services.TryAddSingleton<IServiceExtensionLabelsProvider, ServiceExtensionLabelsProvider>();
            builder.Services.TryAddSingleton<IDiscoverer, Discoverer>();
            builder.Services.AddHostedService<BackgroundWorker>();

            return builder;
        }
    }
}
