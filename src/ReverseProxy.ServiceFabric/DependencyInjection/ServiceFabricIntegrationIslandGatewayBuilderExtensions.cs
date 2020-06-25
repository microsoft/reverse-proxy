// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Configuration.DependencyInjection;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// Extensions for <see cref="IIslandGatewayBuilder"/>
    /// used to register Service Fabric integration components.
    /// </summary>
    public static class ServiceFabricIntegrationIslandGatewayBuilderExtensions
    {
        /// <summary>
        /// Adds the services needed to integrate Service Fabric with the Island Gateway to Dependency Injection.
        /// </summary>
        public static IReverseProxyBuilder AddServiceFabricServiceDiscovery(this IReverseProxyBuilder builder)
        {
            builder.Services.TryAddSingleton<IQueryClientWrapper, QueryClientWrapper>();
            builder.Services.TryAddSingleton<IPropertyManagementClientWrapper, PropertyManagementClientWrapper>();
            builder.Services.TryAddSingleton<IServiceManagementClientWrapper, ServiceManagementClientWrapper>();
            builder.Services.TryAddSingleton<IHealthClientWrapper, HealthClientWrapper>();
            builder.Services.TryAddSingleton<IServiceFabricCaller, CachedServiceFabricCaller>();
            builder.Services.TryAddSingleton<IServiceFabricExtensionConfigProvider, ServiceFabricExtensionConfigProvider>();
            builder.Services.TryAddSingleton<IServiceFabricDiscoveryWorker, ServiceFabricDiscoveryWorker>();
            builder.Services.TryAddSingleton<IServiceDiscovery, ServiceFabricServiceDiscovery>();

            return builder;
        }
    }
}
