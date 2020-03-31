// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.ReverseProxy.Core.Configuration.DependencyInjection
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
    }
}
