// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
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
            builder.Services.AddSingleton<IQueryClientWrapper, QueryClientWrapper>();
            builder.Services.AddSingleton<IPropertyManagementClientWrapper, PropertyManagementClientWrapper>();
            builder.Services.AddSingleton<IServiceManagementClientWrapper, ServiceManagementClientWrapper>();
            builder.Services.AddSingleton<IHealthClientWrapper, HealthClientWrapper>();
            builder.Services.AddSingleton<IServiceFabricCaller, CachedServiceFabricCaller>();
            builder.Services.AddSingleton<IServiceFabricExtensionConfigProvider, ServiceFabricExtensionConfigProvider>();
            builder.Services.AddSingleton<IServiceFabricDiscoveryWorker, ServiceFabricDiscoveryWorker>();
            builder.Services.AddSingleton<IServiceDiscovery, ServiceFabricServiceDiscovery>();

            return builder;
        }
    }
}
